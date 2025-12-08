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
            const int batchSize = 300;
            var insertedRecords = new List<JobMst>();
            var updatedRecords = new List<JobMst>();

            SyncLogger.Log("JobMst", "Sync started at: " + DateTime.Now);

            // Load local data into dictionary for fast lookup
            var localData = await _localContext.JobMst.ToDictionaryAsync(j => j.job!);
            int totalRecords = await _sourceContext.JobMst.CountAsync();

            for (int i = 0; i < totalRecords; i += batchSize)
            {
                var sourceBatch = await _sourceContext.JobMst
                    .OrderBy(j => j.job)
                    .Skip(i)
                    .Take(batchSize)
                    .ToListAsync();

                foreach (var sourceItem in sourceBatch)
                {
                    if (string.IsNullOrEmpty(sourceItem.job))
                        continue;

                    if (localData.TryGetValue(sourceItem.job, out var localItem))
                    {
                        bool needsUpdate = false;

                        // 🔄 Auto-compare all properties dynamically
                        var properties = typeof(JobMst).GetProperties()
                            .Where(p => p.CanRead && p.CanWrite && p.Name != "RecordDate"); // skip RecordDate itself

                        foreach (var prop in properties)
                        {
                            var localValue = prop.GetValue(localItem);
                            var sourceValue = prop.GetValue(sourceItem);

                            // compare values — handle nulls properly
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

                            SyncLogger.Log("JobMst-Updated", new Dictionary<string, object>
                    {
                        { "job", localItem.job! }
                    });
                        }
                    }
                    else
                    {
                        await _localContext.JobMst.AddAsync(sourceItem);
                        insertedRecords.Add(sourceItem);

                        SyncLogger.Log("JobMst-Inserted", new Dictionary<string, object>
                {
                    { "job", sourceItem.job! }
                });
                    }
                }

                await _localContext.SaveChangesAsync();
            }

            SyncLogger.Log("JobMst", $"Sync completed. Inserted: {insertedRecords.Count}, Updated: {updatedRecords.Count}");
            return (insertedRecords, updatedRecords);
        }


        public async Task<List<JobRouteMst>> SyncJobRouteAsync()
        {
            const int batchSize = 300; // You can tune this size based on performance 
            var newRecords = new List<JobRouteMst>();

            // Step 1: Get all source records (use AsNoTracking to reduce EF overhead)
            var sytelineData = await _sourceContext.JobRouteMst
                .AsNoTracking()
                .ToListAsync();

            // Step 2: Get only keys of existing records to compare
            var existingKeys = await _localContext.JobRouteMst
                .Select(r => new { r.Job, r.OperNum })
                .ToListAsync();

            // Step 3: Filter out already existing records
            var filteredNewRecords = sytelineData
                .Where(r => !existingKeys.Any(e => e.Job == r.Job && e.OperNum == r.OperNum))
                .ToList();

            if (filteredNewRecords.Any())
            {
                // Step 4: Insert in batches
                for (int i = 0; i < filteredNewRecords.Count; i += batchSize)
                {
                    var batch = filteredNewRecords.Skip(i).Take(batchSize).ToList();

                    await _localContext.JobRouteMst.AddRangeAsync(batch);
                    await _localContext.SaveChangesAsync();

                    // Logging after batch insert
                    foreach (var item in batch)
                    {
                        SyncLogger.Log("JobRouteMst", new Dictionary<string, object>
                {
                    { "Job", item.Job! },
                    { "OperNum", item.OperNum }
                });

                        newRecords.Add(item); // Add to return list
                    }
                }
            }

            return newRecords;
        }

        public async Task<(List<WcMst> insertedRecords, List<WcMst> updatedRecords)> SyncWcMstAsync()
        {
            const int batchSize = 300;
            var insertedRecords = new List<WcMst>();
            var updatedRecords = new List<WcMst>();

            // Load all local data once into a dictionary for quick comparison
            var localData = await _localContext.WcMst.ToDictionaryAsync(w => w.wc!);

            var totalCount = await _sourceContext.WcMst.CountAsync();

            for (int i = 0; i < totalCount; i += batchSize)
            {
                var batch = await _sourceContext.WcMst
                    .OrderBy(w => w.wc)
                    .Skip(i)
                    .Take(batchSize)
                    .ToListAsync();

                foreach (var sourceItem in batch)
                {
                    if (sourceItem.wc == null) continue;

                    if (localData.TryGetValue(sourceItem.wc, out var localItem))
                    {
                        bool needsUpdate = false;

                        // Use reflection to compare all properties except the key 'wc'
                        foreach (var prop in typeof(WcMst).GetProperties())
                        {
                            if (prop.Name == "wc") continue;

                            var sourceValue = prop.GetValue(sourceItem);
                            var localValue = prop.GetValue(localItem);

                            if (!object.Equals(sourceValue, localValue))
                            {
                                prop.SetValue(localItem, sourceValue);
                                needsUpdate = true;
                            }
                        }

                        if (needsUpdate)
                        {
                            localItem.RecordDate = DateTime.Now; // optional timestamp
                            _localContext.WcMst.Update(localItem);
                            updatedRecords.Add(localItem);

                            SyncLogger.Log("WcMst-Updated", new Dictionary<string, object>
                    {
                        { "wc", localItem.wc! }
                    });
                        }
                    }
                    else
                    {
                        //  New record
                        await _localContext.WcMst.AddAsync(sourceItem);
                        insertedRecords.Add(sourceItem);

                        SyncLogger.Log("WcMst-Inserted", new Dictionary<string, object>
                {
                    { "wc", sourceItem.wc! }
                });
                    }
                }

                await _localContext.SaveChangesAsync();
            }

            return (insertedRecords, updatedRecords);
        }
        

       public async Task<(List<EmployeeMst> insertedRecords, List<EmployeeMst> updatedRecords)> SyncEmployeeMstAsync()
        {
            const int batchSize = 300;
            var insertedRecords = new List<EmployeeMst>();
            var updatedRecords = new List<EmployeeMst>();

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
            }

            return (insertedRecords, updatedRecords);
        }
        

        public async Task<List<JobmatlMst>> SyncJobMatlMstAsync()
        {
            const int batchSize = 500;
            var insertedRecords = new List<JobmatlMst>();

            // Use only Job + Item as key (normalize strings)
            var existingKeys = new HashSet<string>(
                await _localContext.JobMatlMst
                    .Select(m => $"{m.Job!.Trim().ToUpper()}|{m.Item!.Trim().ToUpper()}")
                    .ToListAsync()
            );

            // Get total source count
            var totalCount = await _sourceContext.JobMatlMst.CountAsync();

            for (int i = 0; i < totalCount; i += batchSize)
            {
                var batch = await _sourceContext.JobMatlMst
                    .OrderBy(m => m.Job)        // keep stable ordering to avoid Skip/Take issues
                  //  .ThenBy(m => m.Suffix)
                   // .ThenBy(m => m.OperNum)
                   // .ThenBy(m => m.Sequence)
                    .ThenBy(m => m.Item)
                    .Skip(i)
                    .Take(batchSize)
                    .ToListAsync();

                var newRecords = batch
                    .Where(m => !existingKeys.Contains(
                        $"{m.Job!.Trim().ToUpper()}|{m.Item!.Trim().ToUpper()}"
                    ))
                    .ToList();

                if (newRecords.Any())
                {
                    await _localContext.JobMatlMst.AddRangeAsync(newRecords);
                    await _localContext.SaveChangesAsync();

                    foreach (var item in newRecords)
                    {
                        SyncLogger.Log("JobmatlMst", new Dictionary<string, object>
                {
                    { "Job", item.Job! },
                    { "Item", item.Item! }
                });
                    }

                    insertedRecords.AddRange(newRecords);

                    foreach (var m in newRecords)
                    {
                        existingKeys.Add($"{m.Job!.Trim().ToUpper()}|{m.Item!.Trim().ToUpper()}");
                    }
                }
            }

            return insertedRecords;
        }

         public async Task<List<JobTranMst>> SyncJobTranMstAsync()
        {
            const int batchSize = 300;
            var newRecords = new List<JobTranMst>();

            // Fetch all existing trans_num from the destination (local) DB
            var existingKeys = new HashSet<decimal>(
                await _localContext.JobTranMst.Select(t => t.trans_num).ToListAsync()
           );

            // Stream source data in batches
            var totalCount = await _sourceContext.JobTranMst.CountAsync();

            for (int i = 0; i < totalCount; i += batchSize)
            {
                var batch = await _sourceContext.JobTranMst
                    .OrderBy(t => t.trans_num)
                    .Skip(i)
                     .Take(batchSize)
                    .ToListAsync();

                var newBatch = batch
                     .Where(t => !existingKeys.Contains(t.trans_num))
                    .ToList();

                if (newBatch.Any())
                {
                    await _localContext.JobTranMst.AddRangeAsync(newBatch);
                    await _localContext.SaveChangesAsync();

                    foreach (var item in newBatch)
                    {
                        SyncLogger.Log("JobTranMst", new Dictionary<string, object>
                 {
                 { "trans_num", item.trans_num }
                });

                        // Also add to HashSet so next batches don’t insert again
                        existingKeys.Add(item.trans_num);
                    }

                    newRecords.AddRange(newBatch);
                }
            }

            return newRecords;
        }

         public async Task<List<JobSchMst>> SyncJobSchMstAsync()
         {
             const int batchSize = 300;
             var newRecords = new List<JobSchMst>();

             // Step 1: Get all existing Job+Suffix combinations as HashSet for fast lookup
             var existingKeys = await _localContext.JobSchMst
                 .Select(s => new { s.Job, s.Suffix })
                 .ToListAsync();

             var existingKeySet = new HashSet<string>(
                 existingKeys.Select(e => $"{e.Job}_{e.Suffix}")
             );

             // Step 2: Read source data in batches
             int skip = 0;
             while (true)
             {
                 var batch = await _sourceContext.JobSchMst
                     .OrderBy(s => s.Job) // Add an order to ensure batching consistency
                     .Skip(skip)
                     .Take(batchSize)
                     .ToListAsync();

                if (!batch.Any())
                    break;

                 // Step 3: Filter out existing entries
                var filtered = batch
                     .Where(s => !existingKeySet.Contains($"{s.Job}_{s.Suffix}"))
                    .ToList();

                if (filtered.Any())
                 {
                    await _localContext.JobSchMst.AddRangeAsync(filtered);
                     await _localContext.SaveChangesAsync();
                 newRecords.AddRange(filtered);

                     // Log if needed
                    foreach (var item in filtered)
                    {
                        SyncLogger.Log("JobSchMst", new Dictionary<string, object>
                 {
                 { "Job", item.Job! },
                    { "Suffix", item.Suffix }
                });
                    }
                }

                 skip += batchSize;
             }

             return newRecords;
         } 
         

         public async Task<List<ItemMst>> SyncItemMstAsync()
        {
            const int batchSize = 300;
            var newRecords = new List<ItemMst>();

            // Fetch existing keys (item + description)
            var existingKeys = await _localContext.ItemMst
                .Select(s => new { s.item, s.description })
                .ToListAsync();

            var existingKeySet = new HashSet<string>(
                existingKeys.Select(e => $"{e.item}|{e.description}"),
                StringComparer.OrdinalIgnoreCase
            );

            var insertedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int skip = 0;
            while (true)
            {
                var batch = await _sourceContext.ItemMst

                    .OrderBy(x => x.item)
                    .Skip(skip)
                    .Take(batchSize)
                    .ToListAsync();

                if (!batch.Any())
                    break;

                // Filter only new records
                var filtered = batch
                    .Where(s =>
                    {
                        var key = $"{s.item}|{s.description}";
                        return !existingKeySet.Contains(key) && !insertedThisRun.Contains(key);
                    })
                    .ToList();

                if (filtered.Any())
                {
                    foreach (var item in filtered)
                    {
                        item.item ??= "";
                        item.description ??= ""; // Ensure description is never null
                    }

                    await _localContext.ItemMst.AddRangeAsync(filtered);
                    await _localContext.SaveChangesAsync();
                    newRecords.AddRange(filtered);

                    foreach (var item in filtered)
                    {
                        var key = $"{item.item}|{item.description}";
                        insertedThisRun.Add(key);

                        SyncLogger.Log("ItemMst", new Dictionary<string, object>
                {
                    { "Item", item.item! },
                    { "Description", item.description! }
                });
                    }
                }

                skip += batchSize;
            }

            return newRecords;
        }

       
        // For WomWcEmployee
        public async Task<List<WomWcEmployee>> SyncWomWcEmployeeAsync()
        {
            const int batchSize = 300;
            var newRecords = new List<WomWcEmployee>();

            // Step 1: Get all existing keys (WorkCenter + Employee)
            var existingKeys = await _localContext.WomWcEmployee
                .Select(s => new { s.Wc, s.EmpNum }) // assuming these two columns form unique key
                .ToListAsync();

            var existingKeySet = new HashSet<string>(
                existingKeys.Select(e => $"{e.Wc}_{e.EmpNum}")
            );

            // Step 2: Read source data in batches
            int skip = 0;
            while (true)
            {
                var batch = await _sourceContext.WomWcEmployee
                    .OrderBy(s => s.Wc) // stable batching
                    .Skip(skip)
                    .Take(batchSize)
                    .ToListAsync();

                if (!batch.Any())
                    break;

                // Step 3: Filter out existing entries
                var filtered = batch
                    .Where(s => !existingKeySet.Contains($"{s.Wc}_{s.EmpNum}"))
                    .ToList();

                if (filtered.Any())
                {
                    await _localContext.WomWcEmployee.AddRangeAsync(filtered);
                    await _localContext.SaveChangesAsync();
                    newRecords.AddRange(filtered);

                    // Logging
                    foreach (var item in filtered)
                    {
                        SyncLogger.Log("WomWcEmployee", new Dictionary<string, object>
                {
                    { "Wc", item.Wc! },
                    { "EmpNum", item.EmpNum! }
                });
                    }
                }

                skip += batchSize;
            }

            return newRecords;
        }
    }
}

    
    






    

