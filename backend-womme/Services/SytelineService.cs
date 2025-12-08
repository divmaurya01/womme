using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using WommeAPI.Models;

namespace WommeAPI.Services
{
    public class SytelineService
    {
        private readonly string _connectionString;

        public SytelineService(IConfiguration configuration)
        {
            // Ensure connection string exists, otherwise throw meaningful error
            _connectionString = configuration.GetConnectionString("SytelineConnection")
                ?? throw new InvalidOperationException("Syteline connection string is missing in appsettings.json.");
        }

        /// <summary>
        /// Inserts a JobTranMst record into Syteline's jobtran_mst table.
        /// Uses a dummy employee if emp_num is null or empty to avoid FK errors.
        /// Supports QC jobs by setting trans_type = "M" and assigning a qcgroup.
        /// </summary>
public async Task InsertJobTranAsync(JobTranMst jobTran, JobTranMst? firstStartRow = null, bool isQCJob = false)
{
    if (jobTran == null)
        throw new ArgumentNullException(nameof(jobTran));

    try
    {
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            using (var transaction = conn.BeginTransaction())
            {
                // -----------------------------
                // ALWAYS INSERT → NEVER CHECK
                // -----------------------------

                // Convert start_time to seconds-of-day
                int startTimeInt = (int)jobTran.start_time.Value.TimeOfDay.TotalSeconds;

                // Convert a_hrs to seconds
                int elapsedSeconds = (int)Math.Round((jobTran.a_hrs ?? 0) * 3600);

                // Calculate end_time safely (0–86399 range)
                int endTimeInt = (startTimeInt + elapsedSeconds) % 86400;

                // --------------------------------------
                // ALWAYS INSERT INTO JOBTRAN_MST
                // --------------------------------------
                string insertSql = @"
                INSERT INTO jobtran_mst
                (site_ref, job, suffix, oper_num, next_oper, trans_type,
                 trans_date, start_time, end_time, a_hrs, a_$, qty_complete, qty_moved, qty_scrapped,
                 emp_num, wc, whse, shift, pay_rate, job_rate, issue_parent, complete_op,
                 close_job, posted, Uf_MHSerialNo, Uf_MHStatus, Uf_QCGroup,
                 CreatedBy, UpdatedBy, Uf_MovedOKToStock)
                VALUES
                (@site_ref, @job, @suffix, @oper_num, @next_oper, @trans_type,
                 @trans_date, @start_time, @end_time, @a_hrs, @a_dollar, @qty_complete, @qty_moved, @qty_scrapped,
                 @emp_num, @wc, @whse, @shift, @pay_rate, @job_rate, @issue_parent, @complete_op,
                 @close_job, @posted, @SerialNo, @Status, @QCGroup,
                 @CreatedBy, @UpdatedBy, @MovedOKToStock)";

                using (SqlCommand cmd = new SqlCommand(insertSql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@site_ref", jobTran.site_ref ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@job", jobTran.job);
                    cmd.Parameters.AddWithValue("@suffix", jobTran.suffix);
                    cmd.Parameters.AddWithValue("@oper_num", jobTran.oper_num);
                    cmd.Parameters.AddWithValue("@next_oper", jobTran.next_oper ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@trans_type", jobTran.trans_type ?? "D");
                    cmd.Parameters.AddWithValue("@trans_date", jobTran.trans_date ?? DateTime.Now);

                    cmd.Parameters.AddWithValue("@start_time", startTimeInt);
                    cmd.Parameters.AddWithValue("@end_time", endTimeInt);

                    cmd.Parameters.AddWithValue("@a_hrs", jobTran.a_hrs ?? 0);
                    cmd.Parameters.AddWithValue("@a_dollar", jobTran.a_dollar ?? 0);

                    cmd.Parameters.AddWithValue("@qty_complete", jobTran.qty_complete ?? 0);
                    cmd.Parameters.AddWithValue("@qty_moved", jobTran.qty_moved ?? 0);
                    cmd.Parameters.AddWithValue("@qty_scrapped", jobTran.qty_scrapped ?? 0);

                    cmd.Parameters.AddWithValue("@emp_num", jobTran.emp_num);
                    cmd.Parameters.AddWithValue("@wc", jobTran.wc ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@whse", jobTran.whse ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@shift", jobTran.shift ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@pay_rate", jobTran.pay_rate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@job_rate", jobTran.job_rate ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@issue_parent", jobTran.issue_parent ?? 0);
                    cmd.Parameters.AddWithValue("@complete_op", jobTran.complete_op ?? 0);
                    cmd.Parameters.AddWithValue("@close_job", jobTran.close_job ?? 0);
                    cmd.Parameters.AddWithValue("@posted", jobTran.posted ?? 0);

                    cmd.Parameters.AddWithValue("@SerialNo", jobTran.SerialNo);
                    cmd.Parameters.AddWithValue("@Status", jobTran.status);
                    cmd.Parameters.AddWithValue("@QCGroup", isQCJob ? jobTran.qcgroup : (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@CreatedBy", jobTran.CreatedBy ?? "system");
                    cmd.Parameters.AddWithValue("@UpdatedBy", jobTran.UpdatedBy ?? "system");
                    cmd.Parameters.AddWithValue("@MovedOKToStock", jobTran.Uf_MovedOKToStock ?? 0);

                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Syteline Insert ERROR: " + ex.Message);
    }
}



public async Task<bool> UpdateJobTranInSytelineAsync(UpdateJobLogDto dto)
{
    if (dto == null)
        throw new ArgumentNullException(nameof(dto));

    try
    {
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            Console.WriteLine("[SytelineService] Connection opened.");

            // Convert DateTime to seconds since midnight (Syteline expects seconds)
            int ConvertToSytelineSeconds(DateTime? dt)
            {
                if (!dt.HasValue) return 0;
                return dt.Value.Hour * 3600 + dt.Value.Minute * 60 + dt.Value.Second;
            }

            int startSeconds = ConvertToSytelineSeconds(dto.StartTime);
            int endSeconds = ConvertToSytelineSeconds(dto.EndTime);

            decimal totalHours = 0m;
            if (dto.StartTime.HasValue && dto.EndTime.HasValue)
            {
                totalHours = (decimal)(dto.EndTime.Value - dto.StartTime.Value).TotalHours;
                totalHours = Math.Round(totalHours, 4);
            }

            decimal totalDollar = 0m;
            if (dto.JobRate.HasValue)
                totalDollar = Math.Round(totalHours * dto.JobRate.Value, 2);

            Console.WriteLine($"[SytelineService] Computed times & totals:");
            Console.WriteLine($"  StartTime: {dto.StartTime} -> {startSeconds} secs");
            Console.WriteLine($"  EndTime: {dto.EndTime} -> {endSeconds} secs");
            Console.WriteLine($"  TotalHours: {totalHours}, TotalDollar: {totalDollar}");

            // Ensure trans_num
            decimal transNum = dto.TransNum;
            if (transNum == 0)
            {
                string nextTransSql = "SELECT ISNULL(MAX(CAST(trans_num AS BIGINT)),0) + 1 FROM jobtran_mst";
                using (SqlCommand cmdNext = new SqlCommand(nextTransSql, conn))
                {
                    var obj = await cmdNext.ExecuteScalarAsync();
                    transNum = Convert.ToDecimal(obj);
                }
                Console.WriteLine($"[SytelineService] Computed new TransNum: {transNum}");
            }

            // Determine REG/OT split
            decimal regHours = totalHours <= 8 ? totalHours : 8m;
            decimal otHours = totalHours > 8 ? totalHours - 8m : 0m;

            // Build REG DTO
            var regDto = new UpdateJobLogDto
            {
                TransNum = transNum,
                Job = dto.Job,
                SerialNumber = dto.SerialNumber,
                OperationNumber = dto.OperationNumber,
                WorkCenter = dto.WorkCenter,
                EmpNum = dto.EmpNum,
                SiteRef = dto.SiteRef, 
                Shift = dto.Shift,
                StartTime = dto.StartTime,
                EndTime = dto.StartTime?.AddHours((double)regHours),
                JobRate = dto.JobRate,
                Status = dto.Status,
                UpdatedBy = dto.UpdatedBy,
                TransType = "R",
                QtyComplete = dto.QtyComplete,
                QtyScrapped = dto.QtyScrapped,
                NextOper = dto.NextOper,
                Suffix = dto.Suffix
            };

            // Build OT DTO if needed
            UpdateJobLogDto otDto = null;
            if (otHours > 0)
            {
                otDto = new UpdateJobLogDto
                {
                    TransNum = 0, // will get a new trans_num for OT
                    Job = dto.Job,
                    SerialNumber = dto.SerialNumber,
                    OperationNumber = dto.OperationNumber,
                    WorkCenter = dto.WorkCenter,
                    EmpNum = dto.EmpNum,
                      SiteRef = dto.SiteRef, 
                    Shift = dto.Shift,
                    StartTime = regDto.EndTime,
                    EndTime = dto.EndTime,
                    JobRate = dto.JobRate, // use OT rate if available
                    Status = dto.Status,
                    UpdatedBy = dto.UpdatedBy,
                    TransType = "O",
                    QtyComplete = dto.QtyComplete,
                    QtyScrapped = dto.QtyScrapped,
                    NextOper = dto.NextOper,
                    Suffix = dto.Suffix
                };
            }

            // Function to Insert or Update a row in Syteline
            async Task<bool> UpsertRowAsync(UpdateJobLogDto rowDto)
            {
                decimal rowTransNum = rowDto.TransNum;
                if (rowTransNum == 0)
                {
                    // Generate new trans_num for OT
                    string nextTransSql = "SELECT ISNULL(MAX(CAST(trans_num AS BIGINT)),0) + 1 FROM jobtran_mst";
                    using (SqlCommand cmdNext = new SqlCommand(nextTransSql, conn))
                    {
                        var obj = await cmdNext.ExecuteScalarAsync();
                        rowTransNum = Convert.ToDecimal(obj);
                    }
                    Console.WriteLine($"[SytelineService] OT row new TransNum: {rowTransNum}");
                }

                string existsSql = "SELECT COUNT(1) FROM jobtran_mst WHERE trans_num = @TransNum";
                using (SqlCommand cmdExists = new SqlCommand(existsSql, conn))
                {
                    cmdExists.Parameters.AddWithValue("@TransNum", rowTransNum);
                    bool exists = (int)await cmdExists.ExecuteScalarAsync() > 0;

                    if (exists)
                    {
                        string updateSql = @"
UPDATE jobtran_mst
SET
    site_ref = ISNULL(@SiteRef, site_ref),
    trans_type = @TransType,
    trans_class = ISNULL(@TransClass, trans_class),
    posted = ISNULL(@Posted, posted),
    job = @Job,
    suffix = ISNULL(@Suffix, suffix),
    oper_num = ISNULL(@OperNum, oper_num),
    next_oper = ISNULL(@NextOper, next_oper),
    wc = ISNULL(@WorkCenter, wc),
    emp_num = @EmpNum,
    qty_complete = ISNULL(@QtyComplete, qty_complete),
    qty_scrapped = ISNULL(@QtyScrapped, qty_scrapped),
    a_hrs = @TotalHours,
    a_$ = @TotalDollar,
    job_rate = @JobRate,
    start_time = @StartTime,
    end_time = @EndTime,
    Uf_MHStatus = ISNULL(@Status, Uf_MHStatus),
    shift = ISNULL(@Shift, shift),
    UpdatedBy = @UpdatedBy,
    RecordDate = GETDATE(),
    RowPointer = ISNULL(@RowPointer, RowPointer)
WHERE trans_num = @TransNum
";
                        using (SqlCommand cmd = new SqlCommand(updateSql, conn))
                        {
                           // Instead of hard-coded "MAIN":
                          cmd.Parameters.AddWithValue("@SiteRef", rowDto.SiteRef ?? "DEFAULT");
    
                            cmd.Parameters.AddWithValue("@TransType", rowDto.TransType);
                            cmd.Parameters.AddWithValue("@TransClass", DBNull.Value);
                            cmd.Parameters.AddWithValue("@Posted", 0);
                            cmd.Parameters.AddWithValue("@Job", rowDto.Job ?? "");
                            cmd.Parameters.AddWithValue("@Suffix", (object)rowDto.Suffix ?? 0);
                            cmd.Parameters.AddWithValue("@OperNum", rowDto.OperationNumber);
                            cmd.Parameters.AddWithValue("@NextOper", (object)rowDto.NextOper ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@WorkCenter", rowDto.WorkCenter ?? "");
                            cmd.Parameters.AddWithValue("@EmpNum", rowDto.EmpNum ?? "");
                            cmd.Parameters.AddWithValue("@QtyComplete", (object)rowDto.QtyComplete ?? 0m);
                            cmd.Parameters.AddWithValue("@QtyScrapped", (object)rowDto.QtyScrapped ?? 0m);
                            cmd.Parameters.AddWithValue("@TotalHours", rowDto.TransType == "R" ? regHours : otHours);
                            cmd.Parameters.AddWithValue("@TotalDollar", rowDto.TransType == "R" ? Math.Round(regHours * rowDto.JobRate.Value,2) : Math.Round(otHours * rowDto.JobRate.Value,2));
                            cmd.Parameters.AddWithValue("@JobRate", rowDto.JobRate ?? 0m);
                            cmd.Parameters.AddWithValue("@StartTime", ConvertToSytelineSeconds(rowDto.StartTime));
                            cmd.Parameters.AddWithValue("@EndTime", ConvertToSytelineSeconds(rowDto.EndTime));
                            cmd.Parameters.AddWithValue("@Status", rowDto.Status ?? "");
                            cmd.Parameters.AddWithValue("@Shift", rowDto.Shift ?? "");
                            cmd.Parameters.AddWithValue("@UpdatedBy", rowDto.UpdatedBy ?? "system");
                            cmd.Parameters.AddWithValue("@RowPointer", Guid.NewGuid());
                            cmd.Parameters.AddWithValue("@TransNum", rowTransNum);

                            int rows = await cmd.ExecuteNonQueryAsync();
                            Console.WriteLine($"[SytelineService] ✅ Updated Syteline row {rowTransNum} (rows affected: {rows})");
                            return rows > 0;
                        }
                    }
                    else
                    {
                        string insertSql = @"
INSERT INTO jobtran_mst
(
    site_ref, trans_num, trans_type, trans_class, posted,
    job, suffix, oper_num, next_oper, wc, emp_num,
    qty_complete, qty_scrapped, a_hrs, a_$, job_rate,
    start_time, end_time, Uf_MHStatus, shift, user_code,
    CreatedBy, CreateDate, UpdatedBy, RecordDate, RowPointer
)
VALUES
(
    @SiteRef, @TransNum, @TransType, @TransClass, @Posted,
    @Job, @Suffix, @OperNum, @NextOper, @WorkCenter, @EmpNum,
    @QtyComplete, @QtyScrapped, @TotalHours, @TotalDollar, @JobRate,
    @StartTime, @EndTime, @Status, @Shift, @UserCode,
    @CreatedBy, GETDATE(), @UpdatedBy, GETDATE(), NEWID()
)";
                        using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                        {
                            // Instead of hard-coded "MAIN":
                            cmd.Parameters.AddWithValue("@SiteRef", rowDto.SiteRef ?? "DEFAULT");

                            cmd.Parameters.AddWithValue("@TransNum", rowTransNum);
                            cmd.Parameters.AddWithValue("@TransType", rowDto.TransType);
                            cmd.Parameters.AddWithValue("@TransClass", DBNull.Value);
                            cmd.Parameters.AddWithValue("@Posted", 0);
                            cmd.Parameters.AddWithValue("@Job", rowDto.Job ?? "");
                            cmd.Parameters.AddWithValue("@Suffix", (object)rowDto.Suffix ?? 0);
                            cmd.Parameters.AddWithValue("@OperNum", rowDto.OperationNumber);
                            cmd.Parameters.AddWithValue("@NextOper", (object)rowDto.NextOper ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@WorkCenter", rowDto.WorkCenter ?? "");
                            cmd.Parameters.AddWithValue("@EmpNum", rowDto.EmpNum ?? "");
                            cmd.Parameters.AddWithValue("@QtyComplete", (object)rowDto.QtyComplete ?? 0m);
                            cmd.Parameters.AddWithValue("@QtyScrapped", (object)rowDto.QtyScrapped ?? 0m);
                            cmd.Parameters.AddWithValue("@TotalHours", rowDto.TransType == "R" ? regHours : otHours);
                            cmd.Parameters.AddWithValue("@TotalDollar", rowDto.TransType == "R" ? Math.Round(regHours * rowDto.JobRate.Value,2) : Math.Round(otHours * rowDto.JobRate.Value,2));
                            cmd.Parameters.AddWithValue("@JobRate", rowDto.JobRate ?? 0m);
                            cmd.Parameters.AddWithValue("@StartTime", ConvertToSytelineSeconds(rowDto.StartTime));
                            cmd.Parameters.AddWithValue("@EndTime", ConvertToSytelineSeconds(rowDto.EndTime));
                            cmd.Parameters.AddWithValue("@Status", rowDto.Status ?? "");
                            cmd.Parameters.AddWithValue("@Shift", rowDto.Shift ?? "");
                            cmd.Parameters.AddWithValue("@UserCode", rowDto.UpdatedBy ?? "system");
                            cmd.Parameters.AddWithValue("@CreatedBy", rowDto.UpdatedBy ?? "system");
                            cmd.Parameters.AddWithValue("@UpdatedBy", rowDto.UpdatedBy ?? "system");

                            int rows = await cmd.ExecuteNonQueryAsync();
                            Console.WriteLine($"[SytelineService] ✅ Inserted Syteline row {rowTransNum} (rows affected: {rows})");
                            return rows > 0;
                        }
                    }
                }
            }

            // Sync REG first
            bool regOk = await UpsertRowAsync(regDto);

            // Sync OT if exists
            bool otOk = otDto != null ? await UpsertRowAsync(otDto) : true;

            return regOk && otOk;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SytelineService] ❌ UpdateJobTranInSytelineAsync failed: {ex.Message}");
        if (ex.InnerException != null)
            Console.WriteLine($"  Inner Exception: {ex.InnerException.Message}");
        return false;
    }
}




        public async Task InsertEmployeeAsync(EmployeeMst employee)
        {
            if (employee == null)
                throw new ArgumentNullException(nameof(employee));

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string sql = @"
                INSERT INTO employee_mst
                (
                    emp_num, name, isactive, site_ref,
                    createdby, createdate, updatedby, recorddate,
                    dept, emp_type, pay_freq, mfg_reg_rate, mfg_ot_rate, mfg_dt_rate
                )
                VALUES
                (
                    @emp_num, @name, @isactive, @site_ref,
                    @createdby, @createdate, @updatedby, @recorddate,
                    @dept, @emp_type, @pay_freq, @mfg_reg_rate, @mfg_ot_rate, @mfg_dt_rate
                )
            ";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@emp_num", employee.emp_num ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@name", employee.name ?? (object)DBNull.Value);
                        //  cmd.Parameters.AddWithValue("@passwordhash", employee.PasswordHash ?? (object)DBNull.Value);
                        //  cmd.Parameters.AddWithValue("@roleid", employee.RoleID);
                        cmd.Parameters.AddWithValue("@isactive", employee.IsActive);
                        cmd.Parameters.AddWithValue("@site_ref", employee.site_ref ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@createdby", employee.CreatedBy ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@createdate", employee.CreateDate);
                        cmd.Parameters.AddWithValue("@updatedby", employee.UpdatedBy ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@recorddate", employee.RecordDate);
                        cmd.Parameters.AddWithValue("@dept", employee.dept ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@emp_type", employee.emp_type ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@pay_freq", employee.pay_freq ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@mfg_reg_rate", employee.mfg_reg_rate ?? 0m);
                        cmd.Parameters.AddWithValue("@mfg_ot_rate", employee.mfg_ot_rate ?? 0m);
                        cmd.Parameters.AddWithValue("@mfg_dt_rate", employee.mfg_dt_rate ?? 0m);

                        int rows = await cmd.ExecuteNonQueryAsync();
                        if (rows == 0)
                        {
                            Console.WriteLine("[SytelineService] Warning: No rows inserted into Syteline employee_mst.");
                        }
                        else
                        {
                            Console.WriteLine($"[SytelineService] Employee inserted into Syteline: {employee.emp_num}");
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"[SytelineService] SQL Insert failed: {sqlEx.Message}");
                throw; // Rethrow to API so you can see the error
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SytelineService] Insert failed: {ex.Message}");
                throw;
            }
        }


        public async Task<bool> InsertMachineWcAsync(WomWcMachine machineWc)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                INSERT INTO womwcmachine
                (rowpointer, wc, wcname, machineid, machinedescription, noteexistsflag, recorddate, createdby, updatedby, createdate, inworkflow)
                VALUES (@RowPointer, @Wc, @WcName, @MachineId, @MachineDescription, @NoteExistsFlag, @RecordDate, @CreatedBy, @UpdatedBy, @CreateDate, @InWorkflow)";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@RowPointer", machineWc.RowPointer);
                        command.Parameters.AddWithValue("@Wc", machineWc.Wc ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@WcName", machineWc.WcName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@MachineId", machineWc.MachineId ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@MachineDescription", machineWc.MachineDescription ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@NoteExistsFlag", machineWc.NoteExistsFlag);
                        command.Parameters.AddWithValue("@RecordDate", machineWc.RecordDate);
                        command.Parameters.AddWithValue("@CreatedBy", machineWc.CreatedBy ?? "womme");
                        command.Parameters.AddWithValue("@UpdatedBy", machineWc.UpdatedBy ?? "womme");
                        command.Parameters.AddWithValue("@CreateDate", machineWc.CreateDate);
                        command.Parameters.AddWithValue("@InWorkflow", machineWc.InWorkflow);

                        await command.ExecuteNonQueryAsync();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting into Syteline womwcmachine: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> InsertEmployeeWcAsync(WomWcEmployee employeeWc)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                INSERT INTO wom_wc_employee
                (rowpointer, wc, empnum, description, name, noteexistsflag, recorddate, createdby, updatedby, createdate, inworkflow)
                VALUES
                (@RowPointer, @Wc, @EmpNum, @Description, @Name, @NoteExistsFlag, @RecordDate, @CreatedBy, @UpdatedBy, @CreateDate, @InWorkflow)";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@RowPointer", employeeWc.RowPointer);
                        command.Parameters.AddWithValue("@Wc", employeeWc.Wc ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@EmpNum", employeeWc.EmpNum ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Description", employeeWc.Description ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Name", employeeWc.Name ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@NoteExistsFlag", employeeWc.NoteExistsFlag);
                        command.Parameters.AddWithValue("@RecordDate", employeeWc.RecordDate);
                        command.Parameters.AddWithValue("@CreatedBy", employeeWc.CreatedBy ?? "womme");
                        command.Parameters.AddWithValue("@UpdatedBy", employeeWc.UpdatedBy ?? "womme");
                        command.Parameters.AddWithValue("@CreateDate", employeeWc.CreateDate);
                        command.Parameters.AddWithValue("@InWorkflow", employeeWc.InWorkflow);


                        await command.ExecuteNonQueryAsync();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting into Syteline wom_wc_employee: {ex.Message}");
                return false;
            }
        }

       public async Task<bool> DeleteJobFromSytelineAsync(string jobNumber, string serialNo, int oper_num)
{
    if (string.IsNullOrWhiteSpace(jobNumber))
        throw new ArgumentException("Job number cannot be null or empty.", nameof(jobNumber));

    if (string.IsNullOrWhiteSpace(serialNo))
        throw new ArgumentException("Serial number cannot be null or empty.", nameof(serialNo));

    if (oper_num <= 0)
        throw new ArgumentException("Operation number must be greater than zero.", nameof(oper_num));

    try
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            string sql = @"
                DELETE FROM jobtran_mst
                WHERE job = @JobNumber
                  AND Uf_MHSerialNo = @SerialNo
                  AND oper_num = @OperNum
            ";

            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@JobNumber", jobNumber);
                command.Parameters.AddWithValue("@SerialNo", serialNo);
                command.Parameters.AddWithValue("@OperNum", oper_num);

                int rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    Console.WriteLine($"[SytelineService] Deleted {rowsAffected} jobtran_mst record(s) for Job={jobNumber}, SerialNo={serialNo}, OperNum={oper_num}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[SytelineService] No matching jobtran_mst record found for Job={jobNumber}, SerialNo={serialNo}, OperNum={oper_num}");
                    return false;
                }
            }
        }
    }
    catch (SqlException sqlEx)
    {
        Console.WriteLine($"[SytelineService] SQL Delete failed: {sqlEx.Message}");
        throw;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SytelineService] Delete failed: {ex.Message}");
        throw;
    }
}

//updated by mahima
// public async Task InsertPauseJobTranAsync(JobTranMst lastJob)
// {
//     if (lastJob == null)
//         throw new ArgumentNullException(nameof(lastJob));
 
//     try
//     {
//         using (SqlConnection conn = new SqlConnection(_connectionString))
//         {
//             await conn.OpenAsync();
 
//             // 1️⃣ Check if a pause row already exists for this job/oper/wc/serial
//             string checkSql = @"
//                 SELECT TOP 1 trans_num, a_hrs, a_$, start_time, end_time
//                 FROM jobtran_mst
//                 WHERE job = @Job
//                   AND oper_num = @OperNum
//                   AND wc = @WC
//                   AND Uf_MHSerialNo = @SerialNo
//                   AND Uf_MHStatus = '2'
//                 ORDER BY trans_date DESC";
 
//             using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
//             {
//                 checkCmd.Parameters.AddWithValue("@Job", lastJob.job ?? (object)DBNull.Value);
//                 checkCmd.Parameters.AddWithValue("@OperNum", lastJob.oper_num);
//                 checkCmd.Parameters.AddWithValue("@WC", lastJob.wc ?? (object)DBNull.Value);
//                 checkCmd.Parameters.AddWithValue("@SerialNo", lastJob.SerialNo ?? (object)DBNull.Value);
 
//                 using (var reader = await checkCmd.ExecuteReaderAsync())
//                 {
//                     if (await reader.ReadAsync())
//                     {
//                         // 2️⃣ Pause row exists — update it
//                         var existingTransNum = reader.GetDecimal(0);
//                         var existingAHrs = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
//                         var existingADollar = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
//                         int existingStart = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
 
//                         reader.Close();
 
//                         // Compute new totals
//                         var now = DateTime.UtcNow;
//                         DateTime startTime = lastJob.start_time ?? lastJob.trans_date ?? now;
//                         decimal addedHours = (decimal)((lastJob.end_time ?? now) - startTime).TotalHours;
 
//                         decimal totalAHrs = existingAHrs + addedHours;
//                         decimal totalADollar = existingADollar + ((lastJob.job_rate ?? 0m) * addedHours);
 
//                         int endTimeInt = ((lastJob.end_time ?? now).ToLocalTime().Hour * 100) +
//                                          ((lastJob.end_time ?? now).ToLocalTime().Minute);
 
//                         string updateSql = @"
//                             UPDATE jobtran_mst
//                             SET a_hrs = @AHrs,
//                                 a_$ = @ADollar,
//                                 end_time = @EndTime,
//                                 UpdatedBy = @UpdatedBy,
//                                 recorddate = GETDATE()
//                             WHERE trans_num = @TransNum";
 
//                         using (SqlCommand updateCmd = new SqlCommand(updateSql, conn))
//                         {
//                             updateCmd.Parameters.AddWithValue("@AHrs", totalAHrs);
//                             updateCmd.Parameters.AddWithValue("@ADollar", totalADollar);
//                             updateCmd.Parameters.AddWithValue("@EndTime", endTimeInt);
//                             updateCmd.Parameters.AddWithValue("@UpdatedBy", lastJob.UpdatedBy ?? "system");
//                             updateCmd.Parameters.AddWithValue("@TransNum", existingTransNum);
 
//                             await updateCmd.ExecuteNonQueryAsync();
//                         }
 
//                         return; // ✅ updated existing pause row, exit method
//                     }
//                 }
//             }
 
//             // 3️⃣ Pause row does not exist — insert first pause row
//             string insertSql = @"
//                 INSERT INTO jobtran_mst
//                 (site_ref, job, suffix, oper_num, next_oper, trans_type,
//                  trans_date, start_time, end_time, a_hrs, a_$,
//                  qty_complete, qty_moved, qty_scrapped,
//                  emp_num, wc, whse, shift, pay_rate, job_rate,
//                  issue_parent, complete_op, close_job, posted,
//                  Uf_MHSerialNo, Uf_MHStatus, Uf_QCGroup,
//                  CreatedBy, UpdatedBy, Uf_MovedOKToStock)
//                 VALUES
//                 (@site_ref, @job, @suffix, @oper_num, @next_oper, @trans_type,
//                  @trans_date, @start_time, @end_time, @a_hrs, @a_dollar,
//                  @qty_complete, @qty_moved, @qty_scrapped,
//                  @emp_num, @wc, @whse, @shift, @pay_rate, @job_rate,
//                  @issue_parent, @complete_op, @close_job, @posted,
//                  @Uf_MHSerialNo, @Uf_MHStatus, @Uf_QCGroup,
//                  @CreatedBy, @UpdatedBy, @Uf_MovedOKToStock)";
 
//             using (SqlCommand cmd = new SqlCommand(insertSql, conn))
//             {
// DateTime stLocal = (lastJob.start_time ?? DateTime.UtcNow).ToLocalTime();
//                 DateTime etLocal = (lastJob.end_time ?? DateTime.UtcNow).ToLocalTime();
 
//                 int startInt = stLocal.Hour * 100 + stLocal.Minute;
// int endInt = etLocal.Hour * 100 + etLocal.Minute;

 
//                 cmd.Parameters.AddWithValue("@site_ref", lastJob.site_ref ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@job", lastJob.job ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@suffix", lastJob.suffix);
//                 cmd.Parameters.AddWithValue("@oper_num", lastJob.oper_num);
//                 cmd.Parameters.AddWithValue("@next_oper", lastJob.next_oper ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@trans_type", lastJob.trans_type ?? "D");
//                 cmd.Parameters.AddWithValue("@trans_date", (lastJob.trans_date ?? DateTime.UtcNow).ToLocalTime());
//                 cmd.Parameters.AddWithValue("@start_time", startInt);
//                 cmd.Parameters.AddWithValue("@end_time", endInt);
//                 cmd.Parameters.AddWithValue("@a_hrs", lastJob.a_hrs ?? 0);
//                 cmd.Parameters.AddWithValue("@a_dollar", lastJob.a_dollar ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_complete", lastJob.qty_complete ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_moved", lastJob.qty_moved ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_scrapped", lastJob.qty_scrapped ?? 0);
//                 cmd.Parameters.AddWithValue("@emp_num", lastJob.emp_num ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@wc", lastJob.wc ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@whse", lastJob.whse ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@shift", lastJob.shift ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@pay_rate", lastJob.pay_rate ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@job_rate", lastJob.job_rate ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@issue_parent", lastJob.issue_parent ?? 0);
//                 cmd.Parameters.AddWithValue("@complete_op", lastJob.complete_op ?? 0);
//                 cmd.Parameters.AddWithValue("@close_job", lastJob.close_job ?? 0);
//                 cmd.Parameters.AddWithValue("@posted", lastJob.posted ?? 0);
//                 cmd.Parameters.AddWithValue("@Uf_MHSerialNo", lastJob.SerialNo ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@Uf_MHStatus", "2"); // Pause row status
//                 cmd.Parameters.AddWithValue("@Uf_QCGroup", lastJob.qcgroup ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@CreatedBy", lastJob.UpdatedBy ?? "system");
//                 cmd.Parameters.AddWithValue("@UpdatedBy", lastJob.UpdatedBy ?? "system");
//                 cmd.Parameters.AddWithValue("@Uf_MovedOKToStock", lastJob.Uf_MovedOKToStock ?? 0);
 
//                 await cmd.ExecuteNonQueryAsync();
//             }
//         }
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"[InsertPauseJobTranAsync] Error: {ex.Message}");
//         throw;
//     }
// }   
//-----------------------corrected with transnum but didturned stat tme and end time ------------------------
// public async Task<string> InsertPauseJobTranAsync(JobTranMst lastJob)
// {
//     if (lastJob == null)
//         throw new ArgumentNullException(nameof(lastJob));

//     try
//     {
//         using (SqlConnection conn = new SqlConnection(_connectionString))
//         {
//             await conn.OpenAsync();
//             string sytelineTransNum = "0";

//             // Console.WriteLine("[DEBUG] Opened Syteline connection.");

//             // 1️⃣ Check for existing pause row
//             string checkSql = @"
//                 SELECT TOP 1 trans_num, a_hrs, a_$, start_time, end_time
//                 FROM jobtran_mst
//                 WHERE job = @Job
//                   AND oper_num = @OperNum
//                   AND wc = @WC
//                   AND Uf_MHSerialNo = @SerialNo
//                   AND Uf_MHStatus = '2'
//                 ORDER BY trans_date DESC";

//             using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
//             {
//                 checkCmd.Parameters.AddWithValue("@Job", lastJob.job ?? (object)DBNull.Value);
//                 checkCmd.Parameters.AddWithValue("@OperNum", lastJob.oper_num);
//                 checkCmd.Parameters.AddWithValue("@WC", lastJob.wc ?? (object)DBNull.Value);
//                 checkCmd.Parameters.AddWithValue("@SerialNo", lastJob.SerialNo ?? (object)DBNull.Value);

//                 using (var reader = await checkCmd.ExecuteReaderAsync())
//                 {
//                     if (await reader.ReadAsync())
//                     {
//                         sytelineTransNum = reader.GetDecimal(0).ToString();
//                         decimal existingAHrs = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
//                         decimal existingADollar = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);

//                         // Console.WriteLine($"[DEBUG] Existing pause row found: trans_num={sytelineTransNum}, a_hrs={existingAHrs}, a_dollar={existingADollar}");

//                         reader.Close();

//                         // Calculate new totals
//                         DateTime now = DateTime.UtcNow;
//                         DateTime startTime = lastJob.start_time ?? lastJob.trans_date ?? now;
//                         decimal addedHours = (decimal)((lastJob.end_time ?? now) - startTime).TotalHours;
//                         decimal totalHours = existingAHrs + addedHours;
//                         decimal totalAmount = existingADollar + ((lastJob.job_rate ?? 0m) * addedHours);

//                         int endTimeInt = (lastJob.end_time ?? now).ToLocalTime().Hour * 100 +
//                                          (lastJob.end_time ?? now).ToLocalTime().Minute;

//                         string updateSql = @"
//                             UPDATE jobtran_mst
//                             SET a_hrs = @AHrs,
//                                 a_$ = @ADollar,
//                                 end_time = @EndTime,
//                                 UpdatedBy = @UpdatedBy,
//                                 recorddate = GETDATE()
//                             WHERE trans_num = @TransNum";

//                         using (SqlCommand updateCmd = new SqlCommand(updateSql, conn))
//                         {
//                             updateCmd.Parameters.AddWithValue("@AHrs", totalHours);
//                             updateCmd.Parameters.AddWithValue("@ADollar", totalAmount);
//                             updateCmd.Parameters.AddWithValue("@EndTime", endTimeInt);
//                             updateCmd.Parameters.AddWithValue("@UpdatedBy", lastJob.UpdatedBy ?? "system");
//                             updateCmd.Parameters.AddWithValue("@TransNum", Convert.ToDecimal(sytelineTransNum));

//                             int rows = await updateCmd.ExecuteNonQueryAsync();
//                             // Console.WriteLine($"[DEBUG] Updated existing Syteline pause row, affected rows={rows}");
//                         }

//                         return sytelineTransNum;
//                     }
//                 }
//             }

//             // 2️⃣ Insert new pause row
//             // Console.WriteLine("[DEBUG] No existing pause row found. Inserting new row.");

//             string insertSql = @"
//                 INSERT INTO jobtran_mst
//                 (site_ref, job, suffix, oper_num, next_oper, trans_type,
//                  trans_date, start_time, end_time, a_hrs, a_$,
//                  qty_complete, qty_moved, qty_scrapped,
//                  emp_num, wc, whse, shift, pay_rate, job_rate,
//                  issue_parent, complete_op, close_job, posted,
//                  Uf_MHSerialNo, Uf_MHStatus, Uf_QCGroup,
//                  CreatedBy, UpdatedBy, Uf_MovedOKToStock)
//                 VALUES
//                 (@site_ref, @job, @suffix, @oper_num, @next_oper, @trans_type,
//                  @trans_date, @start_time, @end_time, @a_hrs, @a_dollar,
//                  @qty_complete, @qty_moved, @qty_scrapped,
//                  @emp_num, @wc, @whse, @shift, @pay_rate, @job_rate,
//                  @issue_parent, @complete_op, @close_job, @posted,
//                  @Uf_MHSerialNo, @Uf_MHStatus, @Uf_QCGroup,
//                  @CreatedBy, @UpdatedBy, @Uf_MovedOKToStock)";

//             using (SqlCommand cmd = new SqlCommand(insertSql, conn))
//             {
//                 DateTime stLocal = (lastJob.start_time ?? DateTime.UtcNow).ToLocalTime();
//                 DateTime etLocal = (lastJob.end_time ?? DateTime.UtcNow).ToLocalTime();
//                 int startInt = stLocal.Hour * 100 + stLocal.Minute;
//                 int endInt = etLocal.Hour * 100 + etLocal.Minute;

//                 cmd.Parameters.AddWithValue("@site_ref", lastJob.site_ref ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@job", lastJob.job ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@suffix", lastJob.suffix);
//                 cmd.Parameters.AddWithValue("@oper_num", lastJob.oper_num);
//                 cmd.Parameters.AddWithValue("@next_oper", lastJob.next_oper ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@trans_type", lastJob.trans_type ?? "D");
//                 cmd.Parameters.AddWithValue("@trans_date", (lastJob.trans_date ?? DateTime.UtcNow).ToLocalTime());
//                 cmd.Parameters.AddWithValue("@start_time", startInt);
//                 cmd.Parameters.AddWithValue("@end_time", endInt);
//                 cmd.Parameters.AddWithValue("@a_hrs", lastJob.a_hrs ?? 0);
//                 cmd.Parameters.AddWithValue("@a_dollar", lastJob.a_dollar ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_complete", lastJob.qty_complete ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_moved", lastJob.qty_moved ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_scrapped", lastJob.qty_scrapped ?? 0);
//                 cmd.Parameters.AddWithValue("@emp_num", lastJob.emp_num ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@wc", lastJob.wc ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@whse", lastJob.whse ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@shift", lastJob.shift ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@pay_rate", lastJob.pay_rate ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@job_rate", lastJob.job_rate ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@issue_parent", lastJob.issue_parent ?? 0);
//                 cmd.Parameters.AddWithValue("@complete_op", lastJob.complete_op ?? 0);
//                 cmd.Parameters.AddWithValue("@close_job", lastJob.close_job ?? 0);
//                 cmd.Parameters.AddWithValue("@posted", lastJob.posted ?? 0);
//                 cmd.Parameters.AddWithValue("@Uf_MHSerialNo", lastJob.SerialNo ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@Uf_MHStatus", "2");
//                 cmd.Parameters.AddWithValue("@Uf_QCGroup", lastJob.qcgroup ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@CreatedBy", lastJob.UpdatedBy ?? "system");
//                 cmd.Parameters.AddWithValue("@UpdatedBy", lastJob.UpdatedBy ?? "system");
//                 cmd.Parameters.AddWithValue("@Uf_MovedOKToStock", lastJob.Uf_MovedOKToStock ?? 0);

//                 await cmd.ExecuteNonQueryAsync();
//                 // Console.WriteLine("[DEBUG] Inserted new Syteline pause row, now fetching trans_num...");

//                 // Fetch inserted row's trans_num
//                 string fetchSql = @"
//                     SELECT TOP 1 trans_num
//                     FROM jobtran_mst
//                     WHERE job = @Job
//                       AND oper_num = @OperNum
//                       AND wc = @WC
//                       AND Uf_MHSerialNo = @SerialNo
//                       AND Uf_MHStatus = '2'
//                     ORDER BY trans_date DESC";

//                 using (SqlCommand fetchCmd = new SqlCommand(fetchSql, conn))
//                 {
//                     fetchCmd.Parameters.AddWithValue("@Job", lastJob.job ?? (object)DBNull.Value);
//                     fetchCmd.Parameters.AddWithValue("@OperNum", lastJob.oper_num);
//                     fetchCmd.Parameters.AddWithValue("@WC", lastJob.wc ?? (object)DBNull.Value);
//                     fetchCmd.Parameters.AddWithValue("@SerialNo", lastJob.SerialNo ?? (object)DBNull.Value);

//                     var fetchedTransNum = await fetchCmd.ExecuteScalarAsync();
//                     if (fetchedTransNum != null)
//                         sytelineTransNum = fetchedTransNum.ToString();

//                     // Console.WriteLine($"[DEBUG] Fetched Syteline trans_num: {sytelineTransNum}");
//                 }
//             }

//             return sytelineTransNum;
//         }
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"[InsertPauseJobTranAsync] Error: {ex.Message}");
//         throw;
//     }
// }
//------------------------------------------------------------------------------------------
public async Task<string> InsertPauseJobTranAsync(JobTranMst lastJob)
{
    if (lastJob == null)
        throw new ArgumentNullException(nameof(lastJob));

    string sytelineTransNum = "0";

    try
    {
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();

            // --- 1️⃣ Check for existing pause row ---
            string checkSql = @"
                SELECT TOP 1 trans_num, a_hrs, a_$, start_time, end_time
                FROM jobtran_mst
                WHERE job = @Job
                  AND oper_num = @OperNum
                  AND wc = @WC
                  AND Uf_MHSerialNo = @SerialNo
                  AND Uf_MHStatus = '2'
                ORDER BY trans_date DESC";

            using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
            {
                checkCmd.Parameters.AddWithValue("@Job", lastJob.job ?? (object)DBNull.Value);
                checkCmd.Parameters.AddWithValue("@OperNum", lastJob.oper_num);
                checkCmd.Parameters.AddWithValue("@WC", lastJob.wc ?? (object)DBNull.Value);
                checkCmd.Parameters.AddWithValue("@SerialNo", lastJob.SerialNo ?? (object)DBNull.Value);

                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        sytelineTransNum = reader.GetDecimal(0).ToString();
                        decimal existingAHrs = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                        decimal existingADollar = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);

                        reader.Close();

                        // --- Calculate new hours and total ---
                        DateTime startTime = lastJob.start_time ?? lastJob.trans_date ?? DateTime.UtcNow;
                        DateTime endTime = lastJob.end_time ?? DateTime.UtcNow;

                        decimal addedHours = (decimal)(endTime - startTime).TotalHours;
                        decimal totalHours = existingAHrs + addedHours;
                        decimal totalAmount = existingADollar + ((lastJob.job_rate ?? 0m) * addedHours);

                        // --- Convert end time to seconds ---
                        DateTime etLocal = endTime.ToLocalTime();
                        int endTimeSeconds = (etLocal.Hour * 3600) + (etLocal.Minute * 60) + etLocal.Second;

                        Console.WriteLine($"[DEBUG] Updating existing pause row:");
                        Console.WriteLine($"[DEBUG] EndTime: {etLocal} -> {endTimeSeconds} seconds");
                        Console.WriteLine($"[DEBUG] Total Hours: {totalHours}, Total Amount: {totalAmount}");

                        string updateSql = @"
                            UPDATE jobtran_mst
                            SET a_hrs = @AHrs,
                                a_$ = @ADollar,
                                end_time = @EndTime,
                                UpdatedBy = @UpdatedBy,
                                recorddate = GETDATE()
                            WHERE trans_num = @TransNum";

                        using (SqlCommand updateCmd = new SqlCommand(updateSql, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@AHrs", totalHours);
                            updateCmd.Parameters.AddWithValue("@ADollar", totalAmount);
                            updateCmd.Parameters.AddWithValue("@EndTime", endTimeSeconds);
                            updateCmd.Parameters.AddWithValue("@UpdatedBy", lastJob.UpdatedBy ?? "system");
                            updateCmd.Parameters.AddWithValue("@TransNum", Convert.ToDecimal(sytelineTransNum));

                            await updateCmd.ExecuteNonQueryAsync();
                        }

                        return sytelineTransNum;
                    }
                }
            }

            // --- 2️⃣ Insert new pause row ---
            string insertSql = @"
                INSERT INTO jobtran_mst
                (site_ref, job, suffix, oper_num, next_oper, trans_type,
                 trans_date, start_time, end_time, a_hrs, a_$,
                 qty_complete, qty_moved, qty_scrapped,
                 emp_num, wc, whse, shift, pay_rate, job_rate,
                 issue_parent, complete_op, close_job, posted,
                 Uf_MHSerialNo, Uf_MHStatus, Uf_QCGroup,
                 CreatedBy, UpdatedBy, Uf_MovedOKToStock)
                VALUES
                (@site_ref, @job, @suffix, @oper_num, @next_oper, @trans_type,
                 @trans_date, @start_time, @end_time, @a_hrs, @a_dollar,
                 @qty_complete, @qty_moved, @qty_scrapped,
                 @emp_num, @wc, @whse, @shift, @pay_rate, @job_rate,
                 @issue_parent, @complete_op, @close_job, @posted,
                 @Uf_MHSerialNo, @Uf_MHStatus, @Uf_QCGroup,
                 @CreatedBy, @UpdatedBy, @Uf_MovedOKToStock)";

            using (SqlCommand cmd = new SqlCommand(insertSql, conn))
            {
                DateTime stLocal = (lastJob.start_time ?? lastJob.trans_date ?? DateTime.UtcNow).ToLocalTime();
                DateTime etLocal = (lastJob.end_time ?? DateTime.UtcNow).ToLocalTime();

                int startSeconds = (stLocal.Hour * 3600) + (stLocal.Minute * 60) + stLocal.Second;
                int endSeconds = (etLocal.Hour * 3600) + (etLocal.Minute * 60) + etLocal.Second;

                Console.WriteLine($"[DEBUG] Inserting new pause row:");
                Console.WriteLine($"[DEBUG] StartTime: {stLocal} -> {startSeconds} seconds");
                Console.WriteLine($"[DEBUG] EndTime: {etLocal} -> {endSeconds} seconds");

                cmd.Parameters.AddWithValue("@site_ref", lastJob.site_ref ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@job", lastJob.job ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@suffix", lastJob.suffix);
                cmd.Parameters.AddWithValue("@oper_num", lastJob.oper_num);
                cmd.Parameters.AddWithValue("@next_oper", lastJob.next_oper ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@trans_type", lastJob.trans_type ?? "D");
                cmd.Parameters.AddWithValue("@trans_date", (lastJob.trans_date ?? DateTime.UtcNow).ToLocalTime());
                cmd.Parameters.AddWithValue("@start_time", startSeconds);
                cmd.Parameters.AddWithValue("@end_time", endSeconds);
                cmd.Parameters.AddWithValue("@a_hrs", lastJob.a_hrs ?? 0);
                cmd.Parameters.AddWithValue("@a_dollar", lastJob.a_dollar ?? 0);
                cmd.Parameters.AddWithValue("@qty_complete", lastJob.qty_complete ?? 0);
                cmd.Parameters.AddWithValue("@qty_moved", lastJob.qty_moved ?? 0);
                cmd.Parameters.AddWithValue("@qty_scrapped", lastJob.qty_scrapped ?? 0);
                cmd.Parameters.AddWithValue("@emp_num", lastJob.emp_num ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@wc", lastJob.wc ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@whse", lastJob.whse ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@shift", lastJob.shift ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@pay_rate", lastJob.pay_rate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@job_rate", lastJob.job_rate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@issue_parent", lastJob.issue_parent ?? 0);
                cmd.Parameters.AddWithValue("@complete_op", lastJob.complete_op ?? 0);
                cmd.Parameters.AddWithValue("@close_job", lastJob.close_job ?? 0);
                cmd.Parameters.AddWithValue("@posted", lastJob.posted ?? 0);
                cmd.Parameters.AddWithValue("@Uf_MHSerialNo", lastJob.SerialNo ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Uf_MHStatus", "2");
                cmd.Parameters.AddWithValue("@Uf_QCGroup", lastJob.qcgroup ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", lastJob.UpdatedBy ?? "system");
                cmd.Parameters.AddWithValue("@UpdatedBy", lastJob.UpdatedBy ?? "system");
                cmd.Parameters.AddWithValue("@Uf_MovedOKToStock", lastJob.Uf_MovedOKToStock ?? 0);

                await cmd.ExecuteNonQueryAsync();

                // Fetch inserted row's trans_num for logging
                string fetchSql = @"
                    SELECT TOP 1 trans_num
                    FROM jobtran_mst
                    WHERE job = @Job
                      AND oper_num = @OperNum
                      AND wc = @WC
                      AND Uf_MHSerialNo = @SerialNo
                      AND Uf_MHStatus = '2'
                    ORDER BY trans_date DESC";

                using (SqlCommand fetchCmd = new SqlCommand(fetchSql, conn))
                {
                    fetchCmd.Parameters.AddWithValue("@Job", lastJob.job ?? (object)DBNull.Value);
                    fetchCmd.Parameters.AddWithValue("@OperNum", lastJob.oper_num);
                    fetchCmd.Parameters.AddWithValue("@WC", lastJob.wc ?? (object)DBNull.Value);
                    fetchCmd.Parameters.AddWithValue("@SerialNo", lastJob.SerialNo ?? (object)DBNull.Value);

                    var fetchedTransNum = await fetchCmd.ExecuteScalarAsync();
                    if (fetchedTransNum != null)
                        sytelineTransNum = fetchedTransNum.ToString();
                }

                Console.WriteLine($"[DEBUG] Pause row inserted. TransNum = {sytelineTransNum}");
            }

            return sytelineTransNum;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] InsertPauseJobTranAsync: {ex.Message}");
        throw;
    }
}

//===========================================================================================




//update end by mahima 

// public async Task InsertPauseJobTranAsync(JobTranMst lastJob)
// {
//     if (lastJob == null)
//         throw new ArgumentNullException(nameof(lastJob));

//     // Convert DateTime → seconds (required by Syteline)
//     int ToSeconds(DateTime dt) => (int)dt.TimeOfDay.TotalSeconds;

//     // Normalize time if Kind = Utc
//     DateTime Normalize(DateTime dt) =>
//         dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;

//     try
//     {
//         using (SqlConnection conn = new SqlConnection(_connectionString))
//         {
//             await conn.OpenAsync();

//             // --------------------------------------------------------
//             // 1️⃣ Check if a PAUSE row already exists (Uf_MHStatus = 2)
//             // --------------------------------------------------------
//             string checkSql = @"
//                 SELECT TOP 1 
//                     trans_num, a_hrs, a_$, start_time, end_time
//                 FROM jobtran_mst
//                 WHERE job = @Job
//                   AND oper_num = @OperNum
//                   AND wc = @WC
//                   AND SerialNo = @SerialNo
//                   AND Uf_MHStatus = '2'
//                 ORDER BY trans_date DESC";

//             decimal? existingTransNum = null;
//             decimal existingAHrs = 0;
//             decimal existingADollar = 0;
//             DateTime? existingStart = null;

//             using (SqlCommand check = new SqlCommand(checkSql, conn))
//             {
//                 check.Parameters.AddWithValue("@Job", lastJob.job ?? (object)DBNull.Value);
//                 check.Parameters.AddWithValue("@OperNum", lastJob.oper_num ?? (object)DBNull.Value);
//                 check.Parameters.AddWithValue("@WC", lastJob.wc ?? (object)DBNull.Value);
//                 check.Parameters.AddWithValue("@SerialNo", lastJob.SerialNo ?? (object)DBNull.Value);

//                 using (var rdr = await check.ExecuteReaderAsync())
//                 {
//                     if (await rdr.ReadAsync())
//                     {
//                         existingTransNum = rdr.GetDecimal(0);
//                         existingAHrs = rdr.IsDBNull(1) ? 0 : rdr.GetDecimal(1);
//                         existingADollar = rdr.IsDBNull(2) ? 0 : rdr.GetDecimal(2);

//                         // Convert stored start_time which is in seconds → DateTime
//                         if (!rdr.IsDBNull(3))
//                         {
//                             int s = rdr.GetInt32(3);
//                             existingStart = DateTime.Today.AddSeconds(s);
//                         }
//                     }
//                 }
//             }

//             // --------------------------------------------------------
//             // PREPARE NEW START/END IN LOCAL & SYTELINE FORMAT
//             // --------------------------------------------------------
//             DateTime startLocal = Normalize(lastJob.start_time ?? DateTime.Now);
//             DateTime endLocal = Normalize(lastJob.end_time ?? DateTime.Now);

//             int startSeconds = ToSeconds(startLocal);
//             int endSeconds = ToSeconds(endLocal);

//             DateTime transDate = startLocal.Date;

//             decimal addedHours = (decimal)(endLocal - startLocal).TotalHours;
//             if (addedHours < 0) addedHours = 0;

//             // ====================================================================
//             // 2️⃣ UPDATE EXISTING PAUSE ROW (if exists)
//             // ====================================================================
//             if (existingTransNum.HasValue)
//             {
//                 decimal newTotalHours = existingAHrs + addedHours;
//                 decimal newTotalDollar = existingADollar + ((lastJob.job_rate ?? 0) * addedHours);

//                 string updateSql = @"
//                     UPDATE jobtran_mst
//                     SET 
//                         a_hrs = @AHrs,
//                         a_$ = @ADollar,
//                         end_time = @EndTime,
//                         UpdatedBy = @UpdatedBy,
//                         recorddate = GETDATE()
//                     WHERE trans_num = @TransNum";

//                 using (SqlCommand upd = new SqlCommand(updateSql, conn))
//                 {
//                     upd.Parameters.AddWithValue("@AHrs", newTotalHours);
//                     upd.Parameters.AddWithValue("@ADollar", newTotalDollar);
//                     upd.Parameters.AddWithValue("@EndTime", endSeconds);
//                     upd.Parameters.AddWithValue("@UpdatedBy", lastJob.UpdatedBy ?? "system");
//                     upd.Parameters.AddWithValue("@TransNum", existingTransNum);

//                     await upd.ExecuteNonQueryAsync();
//                 }

//                 return;
//             }

//             // ====================================================================
//             // 3️⃣ INSERT NEW PAUSE ROW
//             // ====================================================================
//             string insertSql = @"
//                 INSERT INTO jobtran_mst
//                 (site_ref, job, suffix, oper_num, next_oper, trans_type,
//                  trans_date, start_time, end_time, a_hrs, a_$,
//                  qty_complete, qty_moved, qty_scrapped,
//                  emp_num, wc, whse, shift, pay_rate, job_rate,
//                  issue_parent, complete_op, close_job, posted,
//                  SerialNo, Uf_MHStatus, qcgroup,
//                  CreatedBy, UpdatedBy, Uf_MovedOKToStock)
//                 VALUES
//                 (@site_ref, @job, @suffix, @oper_num, @next_oper, @trans_type,
//                  @trans_date, @start_time, @end_time, @a_hrs, @a_dollar,
//                  @qty_complete, @qty_moved, @qty_scrapped,
//                  @emp_num, @wc, @whse, @shift, @pay_rate, @job_rate,
//                  @issue_parent, @complete_op, @close_job, @posted,
//                  @SerialNo, @Uf_MHStatus, @qcgroup,
//                  @CreatedBy, @UpdatedBy, @Uf_MovedOKToStock)";

//             using (SqlCommand cmd = new SqlCommand(insertSql, conn))
//             {
//                 cmd.Parameters.AddWithValue("@site_ref", lastJob.site_ref ?? "MAIN");
//                 cmd.Parameters.AddWithValue("@job", lastJob.job ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@suffix", lastJob.suffix ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@oper_num", lastJob.oper_num ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@next_oper", lastJob.next_oper ?? (object)DBNull.Value);

//                 cmd.Parameters.AddWithValue("@trans_type", "D");
//                 cmd.Parameters.AddWithValue("@trans_date", transDate);

//                 cmd.Parameters.AddWithValue("@start_time", startSeconds);
//                 cmd.Parameters.AddWithValue("@end_time", endSeconds);

//                 cmd.Parameters.AddWithValue("@a_hrs", addedHours);
//                 cmd.Parameters.AddWithValue("@a_dollar", (lastJob.job_rate ?? 0) * addedHours);

//                 cmd.Parameters.AddWithValue("@qty_complete", lastJob.qty_complete ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_moved", lastJob.qty_moved ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_scrapped", lastJob.qty_scrapped ?? 0);

//                 cmd.Parameters.AddWithValue("@emp_num", lastJob.emp_num ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@wc", lastJob.wc ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@whse", lastJob.whse ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@shift", lastJob.shift ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@pay_rate", lastJob.pay_rate ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@job_rate", lastJob.job_rate ?? (object)DBNull.Value);

//                 cmd.Parameters.AddWithValue("@issue_parent", lastJob.issue_parent ?? 0);
//                 cmd.Parameters.AddWithValue("@complete_op", lastJob.complete_op ?? 0);
//                 cmd.Parameters.AddWithValue("@close_job", lastJob.close_job ?? 0);
//                 cmd.Parameters.AddWithValue("@posted", lastJob.posted ?? 0);

//                 cmd.Parameters.AddWithValue("@SerialNo", lastJob.SerialNo ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@Uf_MHStatus", "2"); // PAUSE
//                 cmd.Parameters.AddWithValue("@qcgroup", lastJob.qcgroup ?? (object)DBNull.Value);

//                 cmd.Parameters.AddWithValue("@CreatedBy", lastJob.UpdatedBy ?? "system");
//                 cmd.Parameters.AddWithValue("@UpdatedBy", lastJob.UpdatedBy ?? "system");
//                 cmd.Parameters.AddWithValue("@Uf_MovedOKToStock", lastJob.Uf_MovedOKToStock ?? 0);

//                 await cmd.ExecuteNonQueryAsync();
//             }
//         }
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"[InsertPauseJobTranAsync] ERROR → {ex.Message}");
//         throw;
//     }
// }
//--------------complwt job syteline --------------

    // public async Task<string> InsertCompletedJobAsync(JobTranMst jobRow)
    // {
    //     if (jobRow == null) throw new ArgumentNullException(nameof(jobRow));

    //     using (SqlConnection conn = new SqlConnection(_connectionString))
    //     {
    //         await conn.OpenAsync();
    //         string transNum = "0";

    //         // Optional: check if a completed row already exists in Syteline
    //         string checkSql = @"
    //             SELECT TOP 1 trans_num
    //             FROM jobtran_mst
    //             WHERE job = @Job
    //             AND oper_num = @OperNum
    //             AND wc = @WC
    //             AND Uf_MHSerialNo = @SerialNo
    //             AND Uf_MHStatus = '3'
    //             ORDER BY trans_date DESC";

    //         using (var cmd = new SqlCommand(checkSql, conn))
    //         {
    //             cmd.Parameters.AddWithValue("@Job", jobRow.job ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@OperNum", jobRow.oper_num);
    //             cmd.Parameters.AddWithValue("@WC", jobRow.wc ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@SerialNo", jobRow.SerialNo ?? (object)DBNull.Value);

    //             var result = await cmd.ExecuteScalarAsync();
    //             if (result != null)
    //             {
    //                 transNum = result.ToString();
    //                 return transNum; // Already exists
    //             }
    //         }

    //         // Insert new completed job row in Syteline
    //         string insertSql = @"
    //             INSERT INTO jobtran_mst
    //             (site_ref, job, suffix, oper_num, next_oper, trans_type,
    //             trans_date, start_time, end_time, a_hrs, a_$,
    //             emp_num, wc, shift, pay_rate, job_rate,
    //             Uf_MHSerialNo, Uf_MHStatus, CreatedBy, UpdatedBy)
    //             VALUES
    //             (@site_ref, @job, @suffix, @oper_num, @next_oper, @trans_type,
    //             @trans_date, @start_time, @end_time, @a_hrs, @a_dollar,
    //             @emp_num, @wc, @shift, @pay_rate, @job_rate,
    //             @Uf_MHSerialNo, @Uf_MHStatus, @CreatedBy, @UpdatedBy);
    //             SELECT CAST(SCOPE_IDENTITY() AS DECIMAL(18,0));";

    //         using (var cmd = new SqlCommand(insertSql, conn))
    //         {
    //             DateTime stLocal = (jobRow.start_time ?? DateTime.UtcNow).ToLocalTime();
    //             DateTime etLocal = (jobRow.end_time ?? DateTime.UtcNow).ToLocalTime();
    //             int startSeconds = (stLocal.Hour * 3600) + (stLocal.Minute * 60) + stLocal.Second;
    //             int endSeconds = (etLocal.Hour * 3600) + (etLocal.Minute * 60) + etLocal.Second;


    //             cmd.Parameters.AddWithValue("@site_ref", jobRow.site_ref ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@job", jobRow.job ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@suffix", jobRow.suffix);
    //             cmd.Parameters.AddWithValue("@oper_num", jobRow.oper_num);
    //             cmd.Parameters.AddWithValue("@next_oper", jobRow.next_oper ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@trans_type", jobRow.trans_type ?? "R");
    //             cmd.Parameters.AddWithValue("@trans_date", (jobRow.trans_date ?? DateTime.UtcNow).ToLocalTime());
    //             cmd.Parameters.AddWithValue("@start_time", startSeconds);
    //             cmd.Parameters.AddWithValue("@end_time", endSeconds);
    //             cmd.Parameters.AddWithValue("@a_hrs", jobRow.a_hrs ?? 0);
    //             cmd.Parameters.AddWithValue("@a_dollar", jobRow.a_dollar ?? 0);
    //             cmd.Parameters.AddWithValue("@emp_num", jobRow.emp_num ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@wc", jobRow.wc ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@shift", jobRow.shift ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@pay_rate", jobRow.pay_rate ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@job_rate", jobRow.job_rate ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@Uf_MHSerialNo", jobRow.SerialNo ?? (object)DBNull.Value);
    //             cmd.Parameters.AddWithValue("@Uf_MHStatus", "3"); // Completed
    //             cmd.Parameters.AddWithValue("@CreatedBy", jobRow.CreatedBy ?? "system");
    //             cmd.Parameters.AddWithValue("@UpdatedBy", jobRow.UpdatedBy ?? "system");

    //             var insertedTransNum = await cmd.ExecuteScalarAsync();
    //             if (insertedTransNum != null) transNum = insertedTransNum.ToString();
    //         }

    //         return transNum;
    //     }
    // }

// public async Task<string> InsertCompletedJobAsync(JobTranMst jobRow)
// {
//     if (jobRow == null) throw new ArgumentNullException(nameof(jobRow));

//     string transNum = "0";

//     try
//     {
//         using (SqlConnection conn = new SqlConnection(_connectionString))
//         {
//             await conn.OpenAsync();

//             // --- Check if completed job already exists ---
//             string checkSql = @"
//                 SELECT TOP 1 trans_num
//                 FROM jobtran_mst
//                 WHERE job = @Job
//                 AND oper_num = @OperNum
//                 AND wc = @WC
//                 AND Uf_MHSerialNo = @SerialNo
//                 AND Uf_MHStatus = '3'
//                 ORDER BY trans_date DESC";

//             using (var cmd = new SqlCommand(checkSql, conn))
//             {
//                 cmd.Parameters.AddWithValue("@Job", jobRow.job ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@OperNum", jobRow.oper_num);
//                 cmd.Parameters.AddWithValue("@WC", jobRow.wc ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@SerialNo", jobRow.SerialNo ?? (object)DBNull.Value);

//                 var result = await cmd.ExecuteScalarAsync();
//                 if (result != null)
//                 {
//                     transNum = result.ToString();
//                     Console.WriteLine($"[DEBUG] Completed job already exists: TransNum = {transNum}");
//                     return transNum; // Already exists
//                 }
//             }

//             // --- Convert start/end times to seconds ---
//             DateTime stLocal = (jobRow.start_time ?? DateTime.UtcNow).ToLocalTime();
//             DateTime etLocal = (jobRow.end_time ?? DateTime.UtcNow).ToLocalTime();

//             int startSeconds = (stLocal.Hour * 3600) + (stLocal.Minute * 60) + stLocal.Second;
//             int endSeconds = (etLocal.Hour * 3600) + (etLocal.Minute * 60) + etLocal.Second;

//             // Debug output
//             Console.WriteLine($"[DEBUG] StartTime: {stLocal} -> {startSeconds} seconds");
//             Console.WriteLine($"[DEBUG] EndTime: {etLocal} -> {endSeconds} seconds");

//             // --- Insert new completed job row ---
//             string insertSql = @"
//                 INSERT INTO jobtran_mst
//                 (site_ref, job, suffix, oper_num, next_oper, trans_type,
//                 trans_date, start_time, end_time, a_hrs, a_$,
//                 emp_num, wc, shift, pay_rate, job_rate,
//                 Uf_MHSerialNo, Uf_MHStatus, CreatedBy, UpdatedBy)
//                 VALUES
//                 (@site_ref, @job, @suffix, @oper_num, @next_oper, @trans_type,
//                 @trans_date, @start_time, @end_time, @a_hrs, @a_dollar,
//                 @emp_num, @wc, @shift, @pay_rate, @job_rate,
//                 @Uf_MHSerialNo, @Uf_MHStatus, @CreatedBy, @UpdatedBy);
//                 SELECT CAST(SCOPE_IDENTITY() AS DECIMAL(18,0));";

//             using (var cmd = new SqlCommand(insertSql, conn))
//             {
//                 cmd.Parameters.AddWithValue("@site_ref", jobRow.site_ref ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@job", jobRow.job ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@suffix", jobRow.suffix);
//                 cmd.Parameters.AddWithValue("@oper_num", jobRow.oper_num);
//                 cmd.Parameters.AddWithValue("@next_oper", jobRow.next_oper ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@trans_type", jobRow.trans_type ?? "R");
//                 cmd.Parameters.AddWithValue("@trans_date", (jobRow.trans_date ?? DateTime.UtcNow).ToLocalTime());
//                 cmd.Parameters.AddWithValue("@start_time", startSeconds);
//                 cmd.Parameters.AddWithValue("@end_time", endSeconds);
//                 cmd.Parameters.AddWithValue("@a_hrs", jobRow.a_hrs ?? 0);
//                 cmd.Parameters.AddWithValue("@a_dollar", jobRow.a_dollar ?? 0);
//                 cmd.Parameters.AddWithValue("@emp_num", jobRow.emp_num ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@wc", jobRow.wc ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@shift", jobRow.shift ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@pay_rate", jobRow.pay_rate ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@job_rate", jobRow.job_rate ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@Uf_MHSerialNo", jobRow.SerialNo ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@Uf_MHStatus", "3"); // Completed
//                 cmd.Parameters.AddWithValue("@CreatedBy", jobRow.CreatedBy ?? "system");
//                 cmd.Parameters.AddWithValue("@UpdatedBy", jobRow.UpdatedBy ?? "system");

//                 var insertedTransNum = await cmd.ExecuteScalarAsync();
//                 if (insertedTransNum != null)
//                 {
//                     transNum = insertedTransNum.ToString();
//                     Console.WriteLine($"[DEBUG] Inserted new completed job: TransNum = {transNum}");
//                 }
//             }
//         }
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"[ERROR] InsertCompletedJobAsync failed: {ex.Message}");
//         throw;
//     }

//     return transNum;
// }
public async Task<string> InsertCompletedJobAsync(JobTranMst jobRow)
{
    if (jobRow == null) throw new ArgumentNullException(nameof(jobRow));
    string transNum = "0";

    try
    {
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();

            // --- Convert absolute start/end times to relative seconds for Syteline ---
            DateTime start = jobRow.start_time ?? DateTime.UtcNow;
            DateTime end = jobRow.end_time ?? DateTime.UtcNow;

            // Total seconds between start and end (handle cross-midnight)
            double totalSeconds = (end - start).TotalSeconds;
            if (totalSeconds < 0) totalSeconds += 24 * 3600; // crosses midnight

            decimal totalHours = (decimal)(totalSeconds / 3600.0);

            // Split Regular / OT
            decimal regularHours = Math.Min(8m, totalHours);
            decimal otHours = totalHours > 8 ? totalHours - 8m : 0m;

            int regularEndSeconds = (int)(regularHours * 3600);
            int otStartSeconds = regularEndSeconds;
            int otEndSeconds = (int)(totalHours * 3600);

            // --- Function to insert row into Syteline ---
            async Task<string> InsertRow(JobTranMst row, string transType, int startSec, int endSec, decimal hours, decimal rate)
            {
                // Check if completed row of same type already exists
                string checkSql = @"
                    SELECT TOP 1 trans_num
                    FROM jobtran_mst
                    WHERE job = @Job
                      AND oper_num = @OperNum
                      AND wc = @WC
                      AND Uf_MHSerialNo = @SerialNo
                      AND Uf_MHStatus = '3'
                      AND trans_type = @TransType
                    ORDER BY trans_date DESC";

                using (var cmdCheck = new SqlCommand(checkSql, conn))
                {
                    cmdCheck.Parameters.AddWithValue("@Job", row.job ?? (object)DBNull.Value);
                    cmdCheck.Parameters.AddWithValue("@OperNum", row.oper_num);
                    cmdCheck.Parameters.AddWithValue("@WC", row.wc ?? (object)DBNull.Value);
                    cmdCheck.Parameters.AddWithValue("@SerialNo", row.SerialNo ?? (object)DBNull.Value);
                    cmdCheck.Parameters.AddWithValue("@TransType", transType);

                    var existing = await cmdCheck.ExecuteScalarAsync();
                    if (existing != null)
                    {
                        return existing.ToString(); // Already exists
                    }
                }

                // Insert SQL
                string insertSql = @"
                    INSERT INTO jobtran_mst
                    (site_ref, job, suffix, oper_num, next_oper, trans_type,
                     trans_date, start_time, end_time, a_hrs, a_$,
                     emp_num, wc, shift, pay_rate, job_rate,
                     Uf_MHSerialNo, Uf_MHStatus, CreatedBy, UpdatedBy)
                    VALUES
                    (@site_ref, @job, @suffix, @oper_num, @next_oper, @trans_type,
                     @trans_date, @start_time, @end_time, @a_hrs, @a_dollar,
                     @emp_num, @wc, @shift, @pay_rate, @job_rate,
                     @Uf_MHSerialNo, @Uf_MHStatus, @CreatedBy, @UpdatedBy);
                    SELECT CAST(SCOPE_IDENTITY() AS DECIMAL(18,0));";

                using (var cmdInsert = new SqlCommand(insertSql, conn))
                {
                    cmdInsert.Parameters.AddWithValue("@site_ref", row.site_ref ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@job", row.job ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@suffix", row.suffix);
                    cmdInsert.Parameters.AddWithValue("@oper_num", row.oper_num);
                    cmdInsert.Parameters.AddWithValue("@next_oper", row.next_oper ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@trans_type", transType);
                    cmdInsert.Parameters.AddWithValue("@trans_date", (row.trans_date ?? DateTime.UtcNow).ToLocalTime());
                    cmdInsert.Parameters.AddWithValue("@start_time", startSec);
                    cmdInsert.Parameters.AddWithValue("@end_time", endSec);
                    cmdInsert.Parameters.AddWithValue("@a_hrs", hours);
                    cmdInsert.Parameters.AddWithValue("@a_dollar", hours * rate);
                    cmdInsert.Parameters.AddWithValue("@emp_num", row.emp_num ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@wc", row.wc ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@shift", row.shift ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@pay_rate", row.pay_rate ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@job_rate", rate);
                    cmdInsert.Parameters.AddWithValue("@Uf_MHSerialNo", row.SerialNo ?? (object)DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Uf_MHStatus", "3"); // Completed
                    cmdInsert.Parameters.AddWithValue("@CreatedBy", row.CreatedBy ?? "system");
                    cmdInsert.Parameters.AddWithValue("@UpdatedBy", row.UpdatedBy ?? "system");

                    var insertedTransNum = await cmdInsert.ExecuteScalarAsync();
                    return insertedTransNum?.ToString() ?? "0";
                }
            }

            // --- Insert Regular row ---
            transNum = await InsertRow(jobRow, "R", 0, regularEndSeconds, regularHours, jobRow.job_rate ?? 0);

            // --- Insert OT row if exists ---
            if (otHours > 0)
            {
                await InsertRow(jobRow, "O", otStartSeconds, otEndSeconds, otHours, jobRow.job_rate ?? 0);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] InsertCompletedJobAsync failed: {ex.Message}");
        throw;
    }

    return transNum;
}


// public async Task<string> SyncToSytelineAsync(JobTranMst jobRow, string transType, decimal rate)
// {
//     if (jobRow == null)
//         throw new ArgumentNullException(nameof(jobRow));

//     string sytelineTransNum = "0";

//     try
//     {
//         using (SqlConnection conn = new SqlConnection(_connectionString))
//         {
//             await conn.OpenAsync();

//             // 1️⃣ Check if record already exists
//             string checkSql = @"
//                 SELECT TOP 1 trans_num, a_hrs, a_$, start_time, end_time
//                 FROM jobtran_mst
//                 WHERE job = @Job
//                   AND oper_num = @OperNum
//                   AND wc = @WC
//                   AND Uf_MHSerialNo = @SerialNo
//                   AND Uf_MHStatus = @Status
//                 ORDER BY trans_date DESC";

//             using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
//             {
//                 checkCmd.Parameters.AddWithValue("@Job", jobRow.job ?? (object)DBNull.Value);
//                 checkCmd.Parameters.AddWithValue("@OperNum", jobRow.oper_num);
//                 checkCmd.Parameters.AddWithValue("@WC", jobRow.wc ?? (object)DBNull.Value);
//                 checkCmd.Parameters.AddWithValue("@SerialNo", jobRow.SerialNo ?? (object)DBNull.Value);
//                 checkCmd.Parameters.AddWithValue("@Status", jobRow.status ?? "1");

//                 using (var reader = await checkCmd.ExecuteReaderAsync())
//                 {
//                     if (await reader.ReadAsync())
//                     {
//                         // Found existing row
//                         sytelineTransNum = reader.GetDecimal(0).ToString();
//                         decimal existingAHrs = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
//                         decimal existingADollar = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);

//                         reader.Close();

//                         // Calculate new hours
//                         DateTime startTime = jobRow.start_time ?? jobRow.trans_date ?? DateTime.UtcNow;
//                         DateTime endTime = jobRow.end_time ?? DateTime.UtcNow;

//                         decimal addedHours = (decimal)(endTime - startTime).TotalHours;
//                         decimal totalHours = existingAHrs + addedHours;
//                         decimal totalAmount = existingADollar + (rate * addedHours);

//                         // Convert end time to seconds
//                         DateTime etLocal = endTime.ToLocalTime();
//                         int endTimeSeconds = (etLocal.Hour * 3600) + (etLocal.Minute * 60) + etLocal.Second;

//                         string updateSql = @"
//                             UPDATE jobtran_mst
//                             SET a_hrs = @AHrs,
//                                 a_$ = @ADollar,
//                                 end_time = @EndTime,
//                                 UpdatedBy = @UpdatedBy,
//                                 recorddate = GETDATE()
//                             WHERE trans_num = @TransNum";

//                         using (SqlCommand updateCmd = new SqlCommand(updateSql, conn))
//                         {
//                             updateCmd.Parameters.AddWithValue("@AHrs", totalHours);
//                             updateCmd.Parameters.AddWithValue("@ADollar", totalAmount);
//                             updateCmd.Parameters.AddWithValue("@EndTime", endTimeSeconds);
//                             updateCmd.Parameters.AddWithValue("@UpdatedBy", jobRow.UpdatedBy ?? "system");
//                             updateCmd.Parameters.AddWithValue("@TransNum", Convert.ToDecimal(sytelineTransNum));

//                             await updateCmd.ExecuteNonQueryAsync();
//                         }

//                         return sytelineTransNum; // ✔ return updated number
//                     }
//                 }
//             }

//             // 2️⃣ Insert new transaction
//             string insertSql = @"
//                 INSERT INTO jobtran_mst
//                 (site_ref, job, suffix, oper_num, next_oper, trans_type,
//                  trans_date, start_time, end_time, a_hrs, a_$,
//                  qty_complete, qty_moved, qty_scrapped,
//                  emp_num, wc, whse, shift, pay_rate, job_rate,
//                  issue_parent, complete_op, close_job, posted,
//                  Uf_MHSerialNo, Uf_MHStatus, Uf_QCGroup,
//                  CreatedBy, UpdatedBy, Uf_MovedOKToStock)
//                 VALUES
//                 (@site_ref, @job, @suffix, @oper_num, @next_oper, @trans_type,
//                  @trans_date, @start_time, @end_time, @a_hrs, @a_dollar,
//                  @qty_complete, @qty_moved, @qty_scrapped,
//                  @emp_num, @wc, @whse, @shift, @pay_rate, @job_rate,
//                  @issue_parent, @complete_op, @close_job, @posted,
//                  @Uf_MHSerialNo, @Uf_MHStatus, @Uf_QCGroup,
//                  @CreatedBy, @UpdatedBy, @Uf_MovedOKToStock)";

//             using (SqlCommand cmd = new SqlCommand(insertSql, conn))
//             {
//                 DateTime stLocal = (jobRow.start_time ?? jobRow.trans_date ?? DateTime.UtcNow).ToLocalTime();
//                 DateTime etLocal = (jobRow.end_time ?? DateTime.UtcNow).ToLocalTime();

//                 int startSeconds = (stLocal.Hour * 3600) + (stLocal.Minute * 60) + stLocal.Second;
//                 int endSeconds = (etLocal.Hour * 3600) + (etLocal.Minute * 60) + etLocal.Second;

//                 cmd.Parameters.AddWithValue("@site_ref", jobRow.site_ref ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@job", jobRow.job ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@suffix", jobRow.suffix);
//                 cmd.Parameters.AddWithValue("@oper_num", jobRow.oper_num);
//                 cmd.Parameters.AddWithValue("@next_oper", jobRow.next_oper ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@trans_type", transType ?? "O");
//                 cmd.Parameters.AddWithValue("@trans_date", (jobRow.trans_date ?? DateTime.UtcNow).ToLocalTime());
//                 cmd.Parameters.AddWithValue("@start_time", startSeconds);
//                 cmd.Parameters.AddWithValue("@end_time", endSeconds);
//                 cmd.Parameters.AddWithValue("@a_hrs", jobRow.a_hrs ?? 0);
//                 cmd.Parameters.AddWithValue("@a_dollar", jobRow.a_dollar ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_complete", jobRow.qty_complete ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_moved", jobRow.qty_moved ?? 0);
//                 cmd.Parameters.AddWithValue("@qty_scrapped", jobRow.qty_scrapped ?? 0);
//                 cmd.Parameters.AddWithValue("@emp_num", jobRow.emp_num ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@wc", jobRow.wc ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@whse", jobRow.whse ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@shift", jobRow.shift ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@pay_rate", jobRow.pay_rate ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@job_rate", rate);
//                 cmd.Parameters.AddWithValue("@issue_parent", jobRow.issue_parent ?? 0);
//                 cmd.Parameters.AddWithValue("@complete_op", jobRow.complete_op ?? 0);
//                 cmd.Parameters.AddWithValue("@close_job", jobRow.close_job ?? 0);
//                 cmd.Parameters.AddWithValue("@posted", jobRow.posted ?? 0);
//                 cmd.Parameters.AddWithValue("@Uf_MHSerialNo", jobRow.SerialNo ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@Uf_MHStatus", jobRow.status ?? "1");
//                 cmd.Parameters.AddWithValue("@Uf_QCGroup", jobRow.qcgroup ?? (object)DBNull.Value);
//                 cmd.Parameters.AddWithValue("@CreatedBy", jobRow.UpdatedBy ?? "system");
//                 cmd.Parameters.AddWithValue("@UpdatedBy", jobRow.UpdatedBy ?? "system");
//                 cmd.Parameters.AddWithValue("@Uf_MovedOKToStock", jobRow.Uf_MovedOKToStock ?? 0);

//                 await cmd.ExecuteNonQueryAsync();
//             }

//             // 3️⃣ Fetch inserted trans_num so caller gets correct transaction reference
//             string fetchSql = @"
//                 SELECT TOP 1 trans_num
//                 FROM jobtran_mst
//                 WHERE job = @Job
//                   AND oper_num = @OperNum
//                   AND wc = @WC
//                   AND Uf_MHSerialNo = @SerialNo
//                   AND Uf_MHStatus = @Status
//                 ORDER BY trans_date DESC";

//             using (SqlCommand fetchCmd = new SqlCommand(fetchSql, conn))
//             {
//                 fetchCmd.Parameters.AddWithValue("@Job", jobRow.job ?? (object)DBNull.Value);
//                 fetchCmd.Parameters.AddWithValue("@OperNum", jobRow.oper_num);
//                 fetchCmd.Parameters.AddWithValue("@WC", jobRow.wc ?? (object)DBNull.Value);
//                 fetchCmd.Parameters.AddWithValue("@SerialNo", jobRow.SerialNo ?? (object)DBNull.Value);
//                 fetchCmd.Parameters.AddWithValue("@Status", jobRow.status ?? "1");

//                 var fetchedTransNum = await fetchCmd.ExecuteScalarAsync();
//                 if (fetchedTransNum != null)
//                     sytelineTransNum = fetchedTransNum.ToString();
//             }

//             return sytelineTransNum;
//         }
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"[ERROR] SyncToSytelineAsync: {ex.Message}");
//         throw;
//     }
// }





//     }
}


}