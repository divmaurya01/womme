using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlTypes; 
using Microsoft.Data.SqlClient; 
using Microsoft.EntityFrameworkCore;
using WommeAPI.Data;
using WommeAPI.Models;
using WommeAPI.Helpers;

namespace WommeAPI.Services
{
    public class SyncService : ISyncService 
    {
        private readonly SytelineDbContext _sourceContext;
        private readonly AppDbContext _localContext;

        public SyncService(SytelineDbContext sourceContext, AppDbContext localContext)
        {
            _sourceContext = sourceContext;
            _localContext = localContext;
        }

        public async Task SyncDataAsync()
        {
            await SyncJobMstAsync();
            await SyncJobRouteAsync();
            await SyncJobMatlMstAsync();
            await SyncWcMstAsync();
            await SyncEmployeeMstAsync();
            await SyncJobTranMstAsync();
            await SyncJobSchMstAsync();
            await SyncItemMstAsync(); 
            await SyncWomWcEmployeeAsync();
        }


       public async Task<(List<JobMst> insertedRecords, List<JobMst> updatedRecords)> SyncJobMstAsync()
        {
            var insertedRecords = new List<JobMst>();
            var updatedRecords = new List<JobMst>();

            var lastSync = await GetLastSyncDate("JobMst");

            var sourceData = await _sourceContext.JobMst
                .AsNoTracking()
                .Where(x => x.RecordDate > lastSync)
                .ToListAsync();

            var localLookup = (await _localContext.JobMst.ToListAsync())
                .Where(j => !string.IsNullOrEmpty(j.job))
                .ToLookup(j => j.job!);

            foreach (var sourceItem in sourceData)
            {
                if (string.IsNullOrEmpty(sourceItem.job))
                    continue;

                var localItems = localLookup[sourceItem.job];

                if (localItems.Any())
                {
                    foreach (var localItem in localItems)
                    {
                        bool needsUpdate = false;

                        var props = typeof(JobMst).GetProperties()
                            .Where(p => p.CanRead && p.CanWrite && p.Name != "RecordDate");

                        foreach (var prop in props)
                        {
                            var localValue = prop.GetValue(localItem);
                            var sourceValue = prop.GetValue(sourceItem);

                            if (!Equals(localValue, sourceValue))
                            {
                                prop.SetValue(localItem, sourceValue);
                                needsUpdate = true;
                            }
                        }

                        if (needsUpdate)
                        {
                            localItem.RecordDate = DateTime.Now;
                            _localContext.JobMst.Update(localItem);
                            updatedRecords.Add(localItem);
                        }
                    }
                }
                else
                {
                    await _localContext.JobMst.AddAsync(sourceItem);
                    insertedRecords.Add(sourceItem);
                }
            }

            await _localContext.SaveChangesAsync();

            if (sourceData.Any())
                await UpdateLastSyncDate("JobMst", sourceData.Max(x => x.RecordDate));

            return (insertedRecords, updatedRecords);
        }



       public async Task<List<JobRouteMst>> SyncJobRouteAsync()
        {
            var lastSync = await GetLastSyncDate("JobRouteMst");

            var sourceData = await _sourceContext.JobRouteMst
                .AsNoTracking()
                .Where(x => x.RecordDate > lastSync)
                .ToListAsync();

            var existingKeys = await _localContext.JobRouteMst
                .Select(r => $"{r.Job}|{r.OperNum}")
                .ToListAsync();

            var keySet = existingKeys.ToHashSet();
            var newRecords = new List<JobRouteMst>();

            foreach (var src in sourceData)
            {
                var key = $"{src.Job}|{src.OperNum}";
                if (keySet.Contains(key)) continue;

                await _localContext.JobRouteMst.AddAsync(src);
                newRecords.Add(src);
            }

            await _localContext.SaveChangesAsync();

            if (sourceData.Any())
                await UpdateLastSyncDate("JobRouteMst", sourceData.Max(x => x.RecordDate));

            return newRecords;
        }


        public async Task<(List<WcMst>, List<WcMst>)> SyncWcMstAsync()
        {
            var inserted = new List<WcMst>();
            var updated = new List<WcMst>();

            var lastSync = await GetLastSyncDate("WcMst");

            var sourceData = await _sourceContext.WcMst
                .Where(x => x.RecordDate > lastSync)
                .ToListAsync();

            var localData = await _localContext.WcMst.ToDictionaryAsync(w => w.wc!);

            foreach (var src in sourceData)
            {
                if (!localData.TryGetValue(src.wc!, out var local))
                {
                    await _localContext.WcMst.AddAsync(src);
                    inserted.Add(src);
                }
                else
                {
                    local.RecordDate = DateTime.Now;
                    updated.Add(local);
                }
            }

            await _localContext.SaveChangesAsync();

            if (sourceData.Any())
                await UpdateLastSyncDate("WcMst", sourceData.Max(x => x.RecordDate));

            return (inserted, updated);
        }

                

            public async Task<(List<EmployeeMst> insertedRecords, List<EmployeeMst> updatedRecords)> SyncEmployeeMstAsync()
                {
                    const int batchSize = 300;
                    var insertedRecords = new List<EmployeeMst>();
                    var updatedRecords = new List<EmployeeMst>();

                    var lastSync = await GetLastSyncDate("EmployeeMst");
                    var sourceBatch = await _sourceContext.EmployeeMstSource
                        .Where(x => x.RecordDate > lastSync)
                        .ToListAsync();



                    // Load all local employees once into a dictionary
            var localData = await _localContext.EmployeeMst.ToDictionaryAsync(e => e.emp_num!);

            var totalCount = await _sourceContext.EmployeeMstSource.CountAsync();

            for (int i = 0; i < totalCount; i += batchSize)
            {
                var batch = await _sourceContext.EmployeeMstSource
                    .OrderBy(e => e.emp_num)
                    .Skip(i)
                    .Take(batchSize)
                    .ToListAsync();

                foreach (var sourceItem in batch)
                {
                    if (sourceItem.emp_num == null) continue;

                    if (localData.TryGetValue(sourceItem.emp_num, out var localItem))
                    {
                        bool needsUpdate = false;

                        // ✅ Helper method (no ref)
                        void UpdateIfDifferent<T>(Action<T> setAction, T currentValue, T newValue, ref bool changed)
                        {
                            if (!EqualityComparer<T>.Default.Equals(currentValue, newValue))
                            {
                                setAction(newValue);
                                changed = true;
                            }
                        }

                        // ✅ Compare each field and update only if changed
                        UpdateIfDifferent(v => localItem.name = v, localItem.name, sourceItem.name, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.city = v, localItem.city, sourceItem.city, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.state = v, localItem.state, sourceItem.state, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.zip = v, localItem.zip, sourceItem.zip, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.phone = v, localItem.phone, sourceItem.phone, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.ssn = v, localItem.ssn, sourceItem.ssn, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.dept = v, localItem.dept, sourceItem.dept, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.emp_type = v, localItem.emp_type, sourceItem.emp_type, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.pay_freq = v, localItem.pay_freq, sourceItem.pay_freq, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.email_addr = v, localItem.email_addr, sourceItem.email_addr, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.Uf_OfficeLocation = v, localItem.Uf_OfficeLocation, sourceItem.Uf_OfficeLocation, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.Uf_TermReason = v, localItem.Uf_TermReason, sourceItem.Uf_TermReason, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.emp_status = v, localItem.emp_status, sourceItem.emp_status, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.mfg_reg_rate = v, localItem.mfg_reg_rate, sourceItem.mfg_reg_rate, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.mfg_ot_rate = v, localItem.mfg_ot_rate, sourceItem.mfg_ot_rate, ref needsUpdate);
                        UpdateIfDifferent(v => localItem.mfg_dt_rate = v, localItem.mfg_dt_rate, sourceItem.mfg_dt_rate, ref needsUpdate);

                        if (needsUpdate)
                        {
                            localItem.UpdatedBy = sourceItem.UpdatedBy;
                            localItem.RecordDate = DateTime.Now;

                            _localContext.EmployeeMst.Update(localItem);
                            updatedRecords.Add(localItem);

                            SyncLogger.Log("EmployeeMst-Updated", new Dictionary<string, object>
                    {
                        { "emp_num", localItem.emp_num! }
                    });
                        }
                    }
                    else
                    {
                        // 🆕 New Record
                        var newEmp = new EmployeeMst
                        {
                            emp_num = sourceItem.emp_num,
                            site_ref = sourceItem.site_ref,
                            name = sourceItem.name,
                            city = sourceItem.city,
                            state = sourceItem.state,
                            zip = sourceItem.zip,
                            phone = sourceItem.phone,
                            ssn = sourceItem.ssn,
                            dept = sourceItem.dept,
                            emp_type = sourceItem.emp_type,
                            pay_freq = sourceItem.pay_freq,
                            mfg_reg_rate = sourceItem.mfg_reg_rate,
                            mfg_ot_rate = sourceItem.mfg_ot_rate,
                            mfg_dt_rate = sourceItem.mfg_dt_rate,
                            birth_date = sourceItem.birth_date,
                            hire_date = sourceItem.hire_date,
                            raise_date = sourceItem.raise_date,
                            review_date = sourceItem.review_date,
                            term_date = sourceItem.term_date,
                            salary = sourceItem.salary,
                            reg_rate = sourceItem.reg_rate,
                            ot_rate = sourceItem.ot_rate,
                            dt_rate = sourceItem.dt_rate,
                            fwt_num = sourceItem.fwt_num,
                            fwt_dol = sourceItem.fwt_dol,
                            swt_num = sourceItem.swt_num,
                            swt_dol = sourceItem.swt_dol,
                            ytd_fwt = sourceItem.ytd_fwt,
                            ytd_swt = sourceItem.ytd_swt,
                            ytd_med = sourceItem.ytd_med,
                            ytd_tip_cr = sourceItem.ytd_tip_cr,
                            NoteExistsFlag = sourceItem.NoteExistsFlag,
                            RecordDate = DateTime.Now,
                            RowPointer = sourceItem.RowPointer,
                            CreatedBy = sourceItem.CreatedBy,
                            UpdatedBy = sourceItem.UpdatedBy,
                            CreateDate = sourceItem.CreateDate,
                            InWorkflow = sourceItem.InWorkflow,
                            vac_paid = sourceItem.vac_paid,
                            sick_paid = sourceItem.sick_paid,
                            hol_paid = sourceItem.hol_paid,
                            other_paid = sourceItem.other_paid,
                            Uf_Bonus = sourceItem.Uf_Bonus,
                            Uf_last_updated = sourceItem.Uf_last_updated,
                            Uf_new_vac_hr_due = sourceItem.Uf_new_vac_hr_due,
                            Uf_OfficeLocation = sourceItem.Uf_OfficeLocation,
                            Uf_TermReason = sourceItem.Uf_TermReason,
                            Uf_EmpExt = sourceItem.Uf_EmpExt,
                            emp_status = sourceItem.emp_status,
                            email_addr = sourceItem.email_addr,

                            // Local-only columns
                            IsActive = true,
                            RoleID = null,
                            PasswordHash = null,
                            ProfileImage = null
                        };

                        await _localContext.EmployeeMst.AddAsync(newEmp);
                        insertedRecords.Add(newEmp);

                        SyncLogger.Log("EmployeeMst-Inserted", new Dictionary<string, object>
                {
                    { "emp_num", newEmp.emp_num! }
                });
                    }
                }

                await _localContext.SaveChangesAsync();
                if (sourceBatch.Any())
                await UpdateLastSyncDate("EmployeeMst", sourceBatch.Max(x => x.RecordDate));

            }

            return (insertedRecords, updatedRecords);
        }
        

       public async Task<List<JobmatlMst>> SyncJobMatlMstAsync()
        {
            var insertedRecords = new List<JobmatlMst>();

            var lastSync = await GetLastSyncDate("JobMatlMst");

            var sourceData = await _sourceContext.JobMatlMst
                .AsNoTracking()
                .Where(x => x.RecordDate > lastSync)
                .ToListAsync();

            var existingKeys = (await _localContext.JobMatlMst
                .Select(m => $"{m.Job}|{m.Item}")
                .ToListAsync()).ToHashSet();

            foreach (var src in sourceData)
            {
                var key = $"{src.Job}|{src.Item}";
                if (existingKeys.Contains(key)) continue;

                await _localContext.JobMatlMst.AddAsync(src);
                insertedRecords.Add(src);
            }

            await _localContext.SaveChangesAsync();

            if (sourceData.Any())
                await UpdateLastSyncDate("JobMatlMst",
                    sourceData.Max(x => x.RecordDate));

            return insertedRecords;
        }

         public async Task<List<JobTranMst>> SyncJobTranMstAsync()
            {
                var inserted = new List<JobTranMst>();

                var lastSync = await GetLastSyncDate("JobTranMst");

                var sourceData = await _sourceContext.JobTranMst
                    .Where(x => x.RecordDate > lastSync)
                    .ToListAsync();

                var existingKeys = (await _localContext.JobTranMst
                    .Select(t => t.trans_num)
                    .ToListAsync()).ToHashSet();

                foreach (var src in sourceData)
                {
                    if (existingKeys.Contains(src.trans_num)) continue;

                    await _localContext.JobTranMst.AddAsync(src);
                    inserted.Add(src);
                }

                await _localContext.SaveChangesAsync();

                if (sourceData.Any())
                    await UpdateLastSyncDate("JobTranMst",
                        sourceData.Max(x => x.RecordDate));

                return inserted;
            }


         public async Task<List<JobSchMst>> SyncJobSchMstAsync()
        {
            var inserted = new List<JobSchMst>();

            var lastSync = await GetLastSyncDate("JobSchMst");

            var sourceData = await _sourceContext.JobSchMst
                .Where(x => x.RecordDate > lastSync)
                .ToListAsync();

            var existingKeys = (await _localContext.JobSchMst
                .Select(x => $"{x.Job}_{x.Suffix}")
                .ToListAsync()).ToHashSet();

            foreach (var src in sourceData)
            {
                var key = $"{src.Job}_{src.Suffix}";
                if (existingKeys.Contains(key)) continue;

                await _localContext.JobSchMst.AddAsync(src);
                inserted.Add(src);
            }

            await _localContext.SaveChangesAsync();

            if (sourceData.Any())
                await UpdateLastSyncDate("JobSchMst",
                    sourceData.Max(x => x.RecordDate));

            return inserted;
        }

         

         public async Task<List<ItemMst>> SyncItemMstAsync()
            {
                var inserted = new List<ItemMst>();

                var lastSync = await GetLastSyncDate("ItemMst");

                var sourceData = await _sourceContext.ItemMst
                    .Where(x => x.RecordDate > lastSync)
                    .ToListAsync();

                var existingKeys = (await _localContext.ItemMst
                    .Select(x => x.item)
                    .ToListAsync()).ToHashSet();

                foreach (var src in sourceData)
                {
                    if (existingKeys.Contains(src.item)) continue;

                    await _localContext.ItemMst.AddAsync(src);
                    inserted.Add(src);
                }

                await _localContext.SaveChangesAsync();

                if (sourceData.Any())
                    await UpdateLastSyncDate("ItemMst",
                        (DateTime)sourceData.Max(x => x.RecordDate));

                return inserted;
            }


       
        // For WomWcEmployee
        public async Task<List<WomWcEmployee>> SyncWomWcEmployeeAsync()
        {
            var inserted = new List<WomWcEmployee>();

            var lastSync = await GetLastSyncDate("WomWcEmployee");

            var sourceData = await _sourceContext.WomWcEmployee
                .Where(x => x.RecordDate > lastSync)
                .ToListAsync();

            var existingKeys = (await _localContext.WomWcEmployee
                .Select(x => $"{x.Wc}_{x.EmpNum}")
                .ToListAsync()).ToHashSet();

            foreach (var src in sourceData)
            {
                var key = $"{src.Wc}_{src.EmpNum}";
                if (existingKeys.Contains(key)) continue;

                await _localContext.WomWcEmployee.AddAsync(src);
                inserted.Add(src);
            }

            await _localContext.SaveChangesAsync();

            if (sourceData.Any())
                await UpdateLastSyncDate("WomWcEmployee",
                    sourceData.Max(x => x.RecordDate));

            return inserted;
        }




        public async Task<DateTime> GetLastSyncDate(string table)
        {
            var log = await _localContext.SyncLog
                .FirstOrDefaultAsync(x => x.TableName == table);

            return log?.LastSyncDate ?? new DateTime(2025, 10, 1);
        }

        public async Task UpdateLastSyncDate(string table, DateTime date)
        {
            var log = await _localContext.SyncLog
                .FirstOrDefaultAsync(x => x.TableName == table);

            if (log == null)
            {
                await _localContext.SyncLog.AddAsync(new SyncLog
                {
                    TableName = table,
                    LastSyncDate = date
                });
            }
            else
            {
                log.LastSyncDate = date;
            }

            await _localContext.SaveChangesAsync();
        }










    }





}

    
    






    

