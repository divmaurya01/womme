using Microsoft.AspNetCore.Mvc;
using WommeAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using QRCoder;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;
using WommeAPI.Data;
using WommeAPI.Models;
using Microsoft.Data.SqlClient;


namespace WommeAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PostController : ControllerBase
{

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly SytelineService _sytelineService;

    public PostController(AppDbContext context, IConfiguration configuration, SytelineService sytelineService)
    {
        _context = context;
        _configuration = configuration;
        _sytelineService = sytelineService;
    }


    [HttpPost("CreateMachine")]
    public async Task<IActionResult> CreateMachine([FromBody] MachineMaster machine)
    {
        try
        {
            if (machine == null || string.IsNullOrWhiteSpace(machine.MachineNumber))
                return BadRequest(new { message = "Invalid machine data." });

            //  Check if machine number already exists
            var existingMachine = await _context.MachineMaster
                .FirstOrDefaultAsync(m => m.MachineNumber == machine.MachineNumber);

            if (existingMachine != null)
            {
                return Conflict(new { message = $"Machine with number {machine.MachineNumber} already exists." });
            }

            machine.CreatedAt = DateTime.Now;
            machine.UpdatedAt = DateTime.Now;

            _context.MachineMaster.Add(machine);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Machine added successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while adding the machine.", error = ex.Message });
        }
    }


    [HttpPost("AddEmployeeLog")]
    public async Task<IActionResult> AddEmployeeLog([FromBody] EmployeeLog log)
    {
        try
        {
            log.CreatedAt = DateTime.Now;
            log.UpdatedAt = DateTime.Now;

            _context.EmployeeLog.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Employee log added successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error adding employee log.", error = ex.Message });
        }
    }


    [HttpGet("JobTransbyjob")]
    public IActionResult GetJobTrans(string job)
    {
        if (string.IsNullOrEmpty(job))
            return BadRequest("Job parameter is required.");

        var data = _context.JobTranMst
            .Where(j => j.job == job)
            .OrderByDescending(j => j.trans_date) // optional
            .Select(j => new
            {
                j.site_ref,
                j.trans_num,
                j.job,
                j.suffix,
                j.trans_type,
                j.trans_date,
                j.qty_complete,
                j.qty_scrapped,
                j.oper_num,
                j.a_hrs,
                j.next_oper,
                j.emp_num,
                a_dollar = j.a_dollar,
                j.start_time,
                j.end_time,
                j.ind_code,
                j.pay_rate,
                j.qty_moved,
                j.whse,
                j.loc,
                j.user_code,
                j.close_job,
                j.issue_parent,
                j.lot,
                j.complete_op,
                j.pr_rate,
                j.job_rate,
                j.shift,
                j.posted,
                j.low_level,
                j.backflush,
                j.reason_code,
                j.trans_class,
                j.ps_num,
                j.wc,
                j.awaiting_eop,
                j.fixovhd,
                j.varovhd,
                j.cost_code,
                j.co_product_mix,
                j.NoteExistsFlag,
                j.RecordDate,
                j.RowPointer,
                j.CreatedBy,
                j.UpdatedBy,
                j.CreateDate,
                j.InWorkflow,
                j.import_doc_id,
                j.container_num,
                j.parent_lot,
                j.parent_serial,
                j.RESID,
                j.Uf_MovedOKToStock,
                j.Uf_OperCompleted,
                j.Uf_CustName,
                j.Uf_ItemDescription2,
                j.Uf_serial
            })
            .ToList();

        return Ok(data);
    }



   [HttpPost("StartJob")]
    public async Task<IActionResult> StartJob([FromBody] StartJobRequestDto dto)
    {
        if (dto == null)
            return BadRequest("Invalid request");

        try
        {
            var lastJob = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo)
                .OrderByDescending(j => j.trans_date)
                .FirstOrDefaultAsync();

            if (lastJob != null)
            {
                if (lastJob.status == "1")
                    return BadRequest(new { message = "Job is already started." });
                if (lastJob.status == "3")
                    return BadRequest(new { message = "Job is already completed." });
            }

            //  If last job was paused (status=2) and has no end_time, close it
            var lastPaused = await _context.JobTranMst
              .Where(j => j.job == dto.JobNumber
                          && j.oper_num == dto.OperationNumber
                          && j.wc == dto.Wc
                          && j.SerialNo == dto.SerialNo
                          && j.status == "2"
                          && j.end_time == null)
              .OrderByDescending(j => j.trans_date)
              .FirstOrDefaultAsync();

            if (lastPaused != null)
            {
                lastPaused.end_time = DateTime.Now;
                lastPaused.a_hrs = (decimal)((lastPaused.end_time ?? DateTime.Now) - lastPaused.start_time.Value).TotalHours;
                lastPaused.UpdatedBy = dto.loginuser;
                _context.JobTranMst.Update(lastPaused);
                await _context.SaveChangesAsync();
            }             


            var jobRoutes = await _context.JobRouteMst
                .Where(r => r.Job == dto.JobNumber)
                .OrderBy(r => r.OperNum)
                .ToListAsync();

            if (!jobRoutes.Any())
                return NotFound(new { message = "No job route found for this job." });

            var currentIndex = jobRoutes.FindIndex(r => r.OperNum == dto.OperationNumber);
            if (currentIndex == -1)
                return NotFound(new { message = "Operation not found in job route." });

            string? nextOperation = null;
            if (currentIndex + 1 < jobRoutes.Count)
                nextOperation = jobRoutes[currentIndex + 1].OperNum.ToString();

            var employee = await _context.EmployeeMst
                .Where(e => e.emp_num == dto.EmpNum)
                .Select(e => e.mfg_reg_rate)
                .FirstOrDefaultAsync();

            if (employee == 0)
                return NotFound(new { message = "Employee not found or mfg_reg_rate is 0." });

           
            decimal nextTransNum = (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0) + 1;

            var jobTran = new JobTranMst
            {
                site_ref = "DEFAULT",
                trans_num = nextTransNum,
                job = dto.JobNumber,
                SerialNo = dto.SerialNo,
                wc = dto.Wc,
                machine_id = dto.MachineNumber,
                emp_num = dto.EmpNum,
                qty_complete = 0,
                oper_num = dto.OperationNumber,
                next_oper = int.TryParse(nextOperation, out var val) ? val : (int?)null,
                trans_date = DateTime.Now,
                RecordDate = DateTime.Now,
                CreateDate = DateTime.Now,
                CreatedBy = dto.loginuser,
                UpdatedBy = dto.loginuser,
                completed_flag = false,
                suffix = 0,
                trans_type = "D",
                qty_scrapped = 0,
                qty_moved = 0,
                pay_rate = "R",
                whse = "MAIN",
                close_job = 0,
                issue_parent = 0,
                complete_op = 0,
                shift = "1",
                posted = 1,
                job_rate = employee,
                Uf_MovedOKToStock = 0,
                start_time = DateTime.Now,
                status = "1",
                RowPointer = Guid.NewGuid()
            };

            
            _context.JobTranMst.Add(jobTran);
            await _context.SaveChangesAsync();           

            return Ok(new { success = true, message = "Job started successfully.", nextOperation });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error while starting job", error = ex.Message });
        }
    }


    [HttpPost("PauseJob")]
    public async Task<IActionResult> PauseJob([FromBody] StartJobRequestDto dto)
    {
        if (dto == null)
            return BadRequest("Invalid request");

        try
        {
            //  Get last active job
            var lastJob = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo
                            && j.status == "1")
                .OrderByDescending(j => j.trans_date)
                .FirstOrDefaultAsync();

            if (lastJob == null)
                return BadRequest(new { message = "No active job found to pause." });

            var now = DateTime.Now;
            var startTime = lastJob.start_time ?? lastJob.trans_date ?? now;

            TimeSpan duration = now - startTime;
            decimal totalHoursDecimal = (decimal)duration.TotalHours;

            // Get employee rate
            var jobRate = await _context.EmployeeMst
                .Where(e => e.emp_num == dto.EmpNum)
                .Select(e => e.mfg_reg_rate)
                .FirstOrDefaultAsync() ?? 0m;

            decimal a_dollar = totalHoursDecimal * jobRate;

            // Update last active job (status 1)
            lastJob.a_hrs = totalHoursDecimal;
            lastJob.a_dollar = a_dollar;
            lastJob.end_time = now;
            lastJob.job_rate = jobRate;
            lastJob.UpdatedBy = dto.loginuser;

            await _context.SaveChangesAsync();

            //  Always create a new paused row
            decimal nextTransNum = (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0) + 1;

            var pausedJob = new JobTranMst
            {
                site_ref = lastJob.site_ref,
                trans_num = nextTransNum,
                job = lastJob.job,
                SerialNo = lastJob.SerialNo,
                wc = lastJob.wc,
                machine_id = lastJob.machine_id,
                emp_num = lastJob.emp_num,
                qty_complete = lastJob.qty_complete,
                oper_num = lastJob.oper_num,
                next_oper = lastJob.next_oper,
                trans_date = now,
                RecordDate = now,
                CreateDate = now,
                CreatedBy = dto.loginuser,
                UpdatedBy = dto.loginuser,
                completed_flag = false,
                suffix = lastJob.suffix,
                trans_type = lastJob.trans_type,
                qty_scrapped = lastJob.qty_scrapped,
                qty_moved = lastJob.qty_moved,
                pay_rate = lastJob.pay_rate,
                whse = lastJob.whse,
                close_job = lastJob.close_job,
                issue_parent = lastJob.issue_parent,
                complete_op = lastJob.complete_op,
                shift = lastJob.shift,
                posted = lastJob.posted,
                job_rate = jobRate,
                a_hrs = 0,         // paused row starts fresh
                a_dollar = 0,      // paused row starts fresh
                start_time = now,
                end_time = null,
                status = "2",
                RowPointer = Guid.NewGuid(),
                trans_class = lastJob.trans_class,
                item = lastJob.item,
                qcgroup = lastJob.qcgroup,
                Uf_MovedOKToStock = lastJob.Uf_MovedOKToStock
            };

            _context.JobTranMst.Add(pausedJob);
            await _context.SaveChangesAsync();

            // Get last 2 rows
            var lastTwoRows = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                        && j.oper_num == dto.OperationNumber
                        && j.wc == dto.Wc
                        && j.SerialNo == dto.SerialNo)
                .OrderByDescending(j => j.trans_num)
                .Take(2)
                .ToListAsync();

            var latestRow = lastTwoRows.FirstOrDefault();               // status 3
            var secondLastRow = lastTwoRows.Skip(1).FirstOrDefault();   // status 1

            // Check your condition
            if (latestRow?.status == "2" && secondLastRow?.status == "1")
            {
                var sytelineTransNum = await _sytelineService.InsertJobTranAsync(secondLastRow,0);

                if (sytelineTransNum != null)
                {
                    secondLastRow.import_doc_id = sytelineTransNum.Value.ToString();
                    _context.JobTranMst.Update(secondLastRow);
                    await _context.SaveChangesAsync();
                }
            }


            return Ok(new
            {
                success = true,
                message = "Job paused successfully.",
                workedHours = totalHoursDecimal,
                amount = a_dollar
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while pausing job",
                error = ex.Message
            });
        }
    }




//changes end by mahima

  //CompleteJob
  [HttpPost("CompleteJob")]
  public async Task<IActionResult> CompleteJob([FromBody] StartJobRequestDto dto)
    {
        if (dto == null)
            return BadRequest("Invalid request");

        try
        {
            var now = DateTime.Now;

            // Get last job row
            var lastJob = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo)
                .OrderByDescending(j => j.trans_date)
                .FirstOrDefaultAsync();

            if (lastJob == null)
                return NotFound(new { message = "No job found to complete." });

            Console.WriteLine($"LastJob => {lastJob.trans_num}, Status: {lastJob.status}, Start: {lastJob.start_time}, End: {lastJob.end_time}");

            if (lastJob.status == "3")
                return BadRequest(new { message = "Job is already completed." });

            var empRates = await _context.EmployeeMst
                .Where(e => e.emp_num == dto.EmpNum)
                .Select(e => new { RegRate = e.mfg_reg_rate, OtRate = e.mfg_ot_rate })
                .FirstOrDefaultAsync();

            decimal regRate = empRates?.RegRate ?? 0m;
            decimal otRate = empRates?.OtRate ?? 0m;

            // If last job was paused (status=2)
            if (lastJob.status == "2")
            {
                if (lastJob.end_time == null)
                    lastJob.end_time = DateTime.Now;

                lastJob.a_hrs = (decimal)(lastJob.end_time.Value - lastJob.start_time.Value).TotalHours;
                lastJob.UpdatedBy = dto.loginuser;

                _context.JobTranMst.Update(lastJob);
                await _context.SaveChangesAsync();
            }
            else if (lastJob.status == "1") // Running job
            {
                lastJob.end_time = now;
                lastJob.a_hrs = (decimal)(lastJob.end_time.Value - lastJob.start_time.Value).TotalHours;
                lastJob.a_dollar = lastJob.a_hrs * regRate;

                _context.JobTranMst.Update(lastJob);
                await _context.SaveChangesAsync();
            }

            // First job row (start of entire cycle)
            var firstJobRow = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo)
                .OrderBy(j => j.trans_date)
                .FirstOrDefaultAsync();

            DateTime startFrom = firstJobRow?.trans_date ?? now;

            // Get total hours from all running rows
            var runningRows = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                        && j.oper_num == dto.OperationNumber
                        && j.wc == dto.Wc
                        && j.SerialNo == dto.SerialNo
                        && j.status == "1"
                        && j.a_hrs > 0)
                .ToListAsync();

            decimal totalHours = runningRows.Sum(r => r.a_hrs ?? 0m);

            // Split into regular + OT
            decimal regularHours = totalHours > 8 ? 8m : totalHours;
            decimal otHours = totalHours > 8 ? totalHours - 8m : 0m;

            // Create completed regular job row
            decimal nextTransNum = (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0) + 1;

            var completedJob = new JobTranMst
            {
                site_ref = lastJob.site_ref,
                trans_num = nextTransNum,
                job = lastJob.job,
                SerialNo = lastJob.SerialNo,
                wc = lastJob.wc,
                machine_id = lastJob.machine_id,
                emp_num = lastJob.emp_num,
                qty_complete = 1,
                oper_num = lastJob.oper_num,
                next_oper = lastJob.next_oper,
                trans_date = now,
                RecordDate = now,
                CreateDate = now,
                CreatedBy = dto.loginuser,
                UpdatedBy = dto.loginuser,
                completed_flag = true,
                suffix = lastJob.suffix,
                trans_type = "R",
                qty_scrapped = lastJob.qty_scrapped,
                qty_moved = lastJob.qty_moved,
                pay_rate = lastJob.pay_rate,
                whse = lastJob.whse,
                close_job = lastJob.close_job,
                issue_parent = lastJob.issue_parent,
                complete_op = lastJob.complete_op,
                shift = lastJob.shift,
                posted = lastJob.posted,
                job_rate = regRate,
                Uf_MovedOKToStock = lastJob.Uf_MovedOKToStock,
                a_hrs = regularHours,
                a_dollar = regularHours * regRate,
                start_time = startFrom,
                end_time = now,
                status = "3",
                RowPointer = Guid.NewGuid(),
                trans_class = lastJob.trans_class,
                item = lastJob.item,
                qcgroup = lastJob.qcgroup
            };

            _context.JobTranMst.Add(completedJob);
            await _context.SaveChangesAsync();

            // Get last 2 rows
            var lastTwoRows = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                        && j.oper_num == dto.OperationNumber
                        && j.wc == dto.Wc
                        && j.SerialNo == dto.SerialNo)
                .OrderByDescending(j => j.trans_num)
                .Take(2)
                .ToListAsync();

            var latestRow = lastTwoRows.FirstOrDefault();               // status 3
            var secondLastRow = lastTwoRows.Skip(1).FirstOrDefault();   // status 1

            // Check your condition
            if (latestRow?.status == "3" && secondLastRow?.status == "1")
            {
                var sytelineTransNum = await _sytelineService.InsertJobTranAsync(secondLastRow, 1);

                if (sytelineTransNum != null)
                {
                    secondLastRow.import_doc_id = sytelineTransNum.Value.ToString();
                    _context.JobTranMst.Update(secondLastRow);
                    await _context.SaveChangesAsync();
                }
            }

            // CASE 2: latestRow = 3, secondLastRow = 2
            if (latestRow?.status == "3" && secondLastRow?.status == "2")
            {
                // Find the 3rd last row which MUST be status = 1
                var thirdLastRow = await _context.JobTranMst
                    .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo
                            && j.status == "1")
                    .OrderByDescending(j => j.trans_num)
                    .FirstOrDefaultAsync();

                if (thirdLastRow == null)
                {
                    Console.WriteLine("No third last running row found with status 1.");
                }
                else
                {
                    Console.WriteLine($"ThirdLastRow Found => trans_num={thirdLastRow.trans_num}, import_doc_id={thirdLastRow.import_doc_id}");

                    // import_doc_id is the Syteline trans number
                    if (!string.IsNullOrEmpty(thirdLastRow.import_doc_id))
                    {
                        int sytelineTransnumber = int.Parse(thirdLastRow.import_doc_id);

                        // Call SYTELINE to update the row as completed
                        var updateResult = await _sytelineService.UpdateJobTranCompletionAsync(sytelineTransnumber, 1);

                        if (updateResult)
                        {
                            Console.WriteLine("Syteline row updated to complete=1 successfully.");
                        }
                        else
                        {
                            Console.WriteLine("Failed to update Syteline row.");
                        }
                    }
                }
            }



            // If OT exists → insert OT row
            if (otHours > 0)
            {
                var otJob = new JobTranMst
                {
                    site_ref = lastJob.site_ref,
                    trans_num = nextTransNum + 1,
                    job = lastJob.job,
                    SerialNo = lastJob.SerialNo,
                    wc = lastJob.wc,
                    machine_id = lastJob.machine_id,
                    emp_num = lastJob.emp_num,
                    qty_complete = 1,
                    oper_num = lastJob.oper_num,
                    next_oper = lastJob.next_oper,
                    trans_date = now,
                    RecordDate = now,
                    CreateDate = now,
                    CreatedBy = dto.loginuser,
                    UpdatedBy = dto.loginuser,
                    completed_flag = true,
                    suffix = lastJob.suffix,
                    trans_type = "O",
                    qty_scrapped = lastJob.qty_scrapped,
                    qty_moved = lastJob.qty_moved,
                    pay_rate = lastJob.pay_rate,
                    whse = lastJob.whse,
                    close_job = lastJob.close_job,
                    issue_parent = lastJob.issue_parent,
                    complete_op = lastJob.complete_op,
                    shift = lastJob.shift,
                    posted = lastJob.posted,
                    job_rate = otRate,
                    Uf_MovedOKToStock = lastJob.Uf_MovedOKToStock,
                    a_hrs = otHours,
                    a_dollar = otHours * otRate,
                    start_time = startFrom.AddHours((double)regularHours),
                    end_time = now,
                    status = "3",
                    RowPointer = Guid.NewGuid(),
                    trans_class = lastJob.trans_class,
                    item = lastJob.item,
                    qcgroup = lastJob.qcgroup
                };

                _context.JobTranMst.Add(otJob);
                await _context.SaveChangesAsync();

                 // Find only LAST RUNNING row
                var lastRunningRow = await _context.JobTranMst
                    .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo
                            && j.status == "1")
                    .OrderByDescending(j => j.start_time)
                    .FirstOrDefaultAsync();

                if (lastRunningRow != null)
                {
                    // Insert into Syteline (single row)
                    var sytelineTransNum = await _sytelineService.InsertJobTranAsync(lastRunningRow, 0);

                    // Save import_doc_id to local DB
                    if (sytelineTransNum != null)
                    {
                        lastRunningRow.import_doc_id = sytelineTransNum.Value.ToString();
                        _context.JobTranMst.Update(lastRunningRow);
                        await _context.SaveChangesAsync();
                    }
                }


                
            }

            return Ok(new { success = true, message = "Job completed successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while completing job",
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }


    [HttpPost("UpdateJobLog")]
    public async Task<IActionResult> UpdateJobLog([FromBody] UpdateJobLogDto dto)
    {
        if (dto == null)
            return BadRequest("Invalid request data.");

        try
        {
            // 1. Fetch existing local row
            var existingLog = await _context.JobTranMst
                .FirstOrDefaultAsync(j =>
                    j.trans_num == dto.TransNum &&
                    j.job == dto.Job &&
                    j.SerialNo == dto.SerialNumber);

            if (existingLog == null)
                return NotFound($"No job found with Job={dto.Job}, SerialNo={dto.SerialNumber}, TransNum={dto.TransNum}");

            // 2. Get rates
            var empRates = await _context.EmployeeMst
                .Where(e => e.emp_num == dto.EmpNum)
                .Select(e => new { RegRate = e.mfg_reg_rate, OtRate = e.mfg_ot_rate })
                .FirstOrDefaultAsync();

            decimal regRate = empRates?.RegRate ?? 0m;
            decimal otRate = empRates?.OtRate ?? 0m;

            // 3. Update basic fields on existingLog
            existingLog.SerialNo = dto.SerialNumber;
            existingLog.job = dto.Job;
            existingLog.oper_num = dto.OperationNumber;
            existingLog.wc = dto.WorkCenter;
            existingLog.status = dto.Status;
            existingLog.job_rate = dto.JobRate;
            existingLog.shift = dto.Shift;
            existingLog.emp_num = dto.EmpNum;
            existingLog.machine_id = dto.MachineNum;
            existingLog.UpdatedBy = dto.UpdatedBy;

            if (dto.StartTime.HasValue)
                existingLog.start_time = dto.StartTime;

            if (dto.EndTime.HasValue)
                existingLog.end_time = dto.EndTime;

            // 4. Pause-case (status 3 -> 2) — keep as-is but do not zero-out unless intended.
            if (existingLog.status?.Trim() == "2" && dto.Status == "2" && dto.EndTime == null)
            {
                // if your business requires zeroing hours here, keep; otherwise comment out these lines.
                existingLog.a_hrs = 0;
                existingLog.a_dollar = 0;
                _context.JobTranMst.Update(existingLog);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Status changed to Pause successfully (no hour change)." });
            }

            // 5. If both start & end exist -> compute hours and split into REG/OT
            if (existingLog.start_time.HasValue && existingLog.end_time.HasValue)
            {
                decimal totalHours = (decimal)(existingLog.end_time.Value - existingLog.start_time.Value).TotalHours;
                totalHours = Math.Round(totalHours, 4);

                decimal regularHours = totalHours <= 8 ? totalHours : 8m;
                decimal otHours = totalHours > 8 ? totalHours - 8m : 0m;

                // Update regular row: a_hrs capped at 8 and end_time set to start + regularHours
                existingLog.a_hrs = regularHours;
                existingLog.a_dollar = regularHours * regRate;
                existingLog.end_time = existingLog.start_time.Value.AddHours((double)regularHours);

                _context.JobTranMst.Update(existingLog);

                // Prepare to create OT row if needed
                JobTranMst otRow = null;
                if (otHours > 0)
                {
                    // compute next trans_num locally (avoid collision using local max)
                    decimal nextTrans = (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0) + 1;

                    otRow = new JobTranMst
                    {
                        site_ref = existingLog.site_ref,
                        trans_num = nextTrans,
                        job = existingLog.job,
                        SerialNo = existingLog.SerialNo,
                        wc = existingLog.wc,
                        machine_id = existingLog.machine_id,
                        emp_num = existingLog.emp_num,
                        qty_complete = existingLog.qty_complete,
                        qty_scrapped = existingLog.qty_scrapped,
                        oper_num = existingLog.oper_num,
                        next_oper = existingLog.next_oper,
                        trans_date = DateTime.Now,
                        RecordDate = DateTime.Now,
                        CreateDate = DateTime.Now,
                        CreatedBy = dto.UpdatedBy,
                        UpdatedBy = dto.UpdatedBy,
                        completed_flag = existingLog.completed_flag,
                        suffix = existingLog.suffix,
                        trans_type = "O",
                        job_rate = otRate,
                        a_hrs = otHours,
                        a_dollar = otHours * otRate,
                        start_time = existingLog.end_time, // immediately after REG hours
                        end_time = dto.EndTime,            // original manual end time
                        status = existingLog.status,
                        shift = existingLog.shift,
                        posted = existingLog.posted ?? 0,
                        RowPointer = Guid.NewGuid(),
                        trans_class = existingLog.trans_class,
                        item = existingLog.item,
                        qcgroup = existingLog.qcgroup
                    };

                    _context.JobTranMst.Add(otRow);
                }

                // 6. Save local changes (REG and OT) as a single DB transaction
                await _context.SaveChangesAsync();

                // 7. Sync to Syteline: update REG first, then insert/update OT
                // Build REG dto with TransNum set
                var regDto = new UpdateJobLogDto
                {
                    TransNum = existingLog.trans_num,
                    Job = existingLog.job,
                    SerialNumber = existingLog.SerialNo,
                    OperationNumber = existingLog.oper_num ?? 0,
                    WorkCenter = existingLog.wc,
                    EmpNum = existingLog.emp_num,
                    Shift = existingLog.shift,
                    StartTime = existingLog.start_time,
                    EndTime = existingLog.end_time,
                    JobRate = existingLog.job_rate,
                    Status = existingLog.status,
                    UpdatedBy = existingLog.UpdatedBy,
                    Item = existingLog.item,
                    Suffix = existingLog.suffix,
                    NextOper = existingLog.next_oper,
                    QtyComplete = existingLog.qty_complete,
                    QtyScrapped = existingLog.qty_scrapped
                };

                // Update REG in Syteline (best-effort)
                //var regOk = await _sytelineService.UpdateJobTranInSytelineAsync(regDto);

                // If OT row created, map and sync it
                if (otRow != null)
                {
                    var otDto = new UpdateJobLogDto
                    {
                        TransNum = otRow.trans_num,   // CRITICAL: include trans_num
                        Job = otRow.job,
                        SerialNumber = otRow.SerialNo,
                        OperationNumber = otRow.oper_num ?? 0,
                        WorkCenter = otRow.wc,
                        EmpNum = otRow.emp_num,
                        Shift = otRow.shift,
                        StartTime = otRow.start_time,
                        EndTime = otRow.end_time,
                        JobRate = otRow.job_rate,
                        Status = otRow.status,
                        UpdatedBy = otRow.UpdatedBy,
                        Item = otRow.item,
                        TransType = "O",
                        Suffix = otRow.suffix,
                        NextOper = otRow.next_oper,
                        QtyComplete = otRow.qty_complete,
                        QtyScrapped = otRow.qty_scrapped
                    };

                    //var otOk = await _sytelineService.UpdateJobTranInSytelineAsync(otDto);
                }

                return Ok(new
                {
                    message = "Updated successfully (REG + OT split)",
                    regularHours = regularHours,
                    otHours = otHours
                });
            }

            // no start/end change
            _context.JobTranMst.Update(existingLog);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Record updated successfully (no hour change)." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
        }
    }


    [HttpPost("EmpMechCodeChecker")]
    public async Task<IActionResult> EmpMechCodeChecker([FromBody] EmpMechCheckRequestDto request)
    {
        try
        {
            // 🔹 Step 1: Get latest EmployeeLog for this job/operation/trans/serial
            var log = await _context.EmployeeLog
                .Where(e => e.JobNumber == request.Job
                            && e.OperationNumber == request.Operation
                            && e.TransNumber == request.TransNum
                            && e.serialNo == request.SerialNo)
                .OrderByDescending(e => e.EntryNo) // 👈 ensures last inserted
                .FirstOrDefaultAsync();

            if (log == null)
            {
                return Ok(new
                {
                    employee = "",
                    employeeName = "",
                    machine = "",
                    machineDesc = "",
                    status = "Unknown"
                });
            }

            // 🔹 Step 2: Lookup Employee details
            var employee = await _context.EmployeeMst
                .Where(emp => emp.emp_num == log.EmployeeCode)
                .Select(emp => new { emp.emp_num, emp.name })
                .FirstOrDefaultAsync();

            // 🔹 Step 3: Lookup Machine details
            var machine = await _context.MachineMaster
                .Where(m => m.MachineNumber == log.MachineNumber)
                .Select(m => new { m.MachineNumber, m.MachineDescription })
                .FirstOrDefaultAsync();

            // 🔹 Step 4: Translate StatusID
            string statusText = log.StatusID switch
            {
                1 => "Started",
                2 => "Paused",
                3 => "Completed",
                _ => "Unknown"
            };

            // 🔹 Step 5: Return all EmployeeLog details + name + machine + status
            return Ok(new
            {
                // From EmployeeLog

                log.JobNumber,
                log.OperationNumber,
                log.TransNumber,
                log.serialNo,
                log.EmployeeCode,
                log.MachineNumber,
                log.StatusID,
                Status = statusText,
                log.StatusTime,
                log.CreatedAt,
                log.UpdatedAt,

                // From EmployeeMst
                employeeName = employee?.name ?? "",

                // From MachineMaster
                machineDesc = machine?.MachineDescription ?? ""
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error checking employee/machine", error = ex.Message });
        }
    }


    [HttpPost("CheckJobStatus")]
    public async Task<IActionResult> CheckJobStatus([FromBody] EmpMechCheckRequestDto request)
    {
        try
        {
            // 🔹 Step 1: Get ALL EmployeeLog entries for this job/operation/trans/serial
            var logs = await _context.EmployeeLog
                .Where(e => e.JobNumber == request.Job
                            && e.OperationNumber == request.Operation
                            && e.TransNumber == request.TransNum
                            && e.serialNo == request.SerialNo)
                .OrderBy(e => e.EntryNo)  // oldest → latest
                .ToListAsync();

            if (!logs.Any())
            {
                return Ok(new { message = "No logs found for this job/operation/transaction/serial." });
            }

            // 🔹 Step 2: Translate StatusID for each row
            var result = logs.Select(log => new
            {
                log.EntryNo,
                log.JobNumber,
                log.OperationNumber,
                log.TransNumber,
                log.serialNo,
                log.EmployeeCode,
                log.MachineNumber,
                log.StatusID,
                Status = log.StatusID switch
                {
                    1 => "Started",
                    2 => "Paused",
                    3 => "Completed",
                    _ => "Unknown"
                },
                log.StatusTime,
                log.CreatedAt,
                log.UpdatedAt
            });

            // 🔹 Step 3: Return all logs
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error checking job status", error = ex.Message });
        }
    }



    [HttpPost("AddEmployee")]
    public async Task<IActionResult> AddEmployee([FromBody] EmployeeDto dto)
    {
        try
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.EmpNum))
                return BadRequest(new { message = "Invalid employee data." });

            var employee = new EmployeeMst
            {
                emp_num = dto.EmpNum,
                name = dto.Name,
                PasswordHash = dto.PasswordHash,
                RoleID = dto.RoleID,
                IsActive = dto.IsActive ?? true,
                site_ref = dto.site_ref,
                CreatedBy = dto.CreatedBy,
                CreateDate = DateTime.Now,
                UpdatedBy = dto.CreatedBy ?? "system",
                RecordDate = DateTime.Now,
                dept = dto.depart,
                emp_type = dto.emp_type,
                pay_freq = dto.pay_freq,
                mfg_reg_rate = dto.mfg_reg_rate,
                mfg_ot_rate = dto.mfg_ot_rate,
                mfg_dt_rate = dto.mfg_dt_rate
            };

            _context.EmployeeMst.Add(employee);
            await _context.SaveChangesAsync();

            // ✅ Push to Syteline after successful local insert
            var sytelineService = new SytelineService(_configuration);
            await sytelineService.InsertEmployeeAsync(employee);

            return Ok(new { message = "Employee created successfully and synced to Syteline." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Server error", error = ex.Message });
        }
    }


    [HttpPost("CheckPrevJob")]
    public async Task<IActionResult> CheckPrevJob([FromBody] JobStartRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Job) || string.IsNullOrWhiteSpace(request.SerialNo))
                return BadRequest(new { message = "Invalid job start request." });

            string serialNo = request.SerialNo.Trim(); // e.g., "13_5"

            // Split serial into base and index
            var parts = serialNo.Split('_');
            if (parts.Length != 2)
                return BadRequest(new { message = "Invalid serial number format." });

            string baseSerial = parts[0];       // "13"
            string serialIndexStr = parts[1];   // "5"

            if (!int.TryParse(serialIndexStr, out int serialIndex))
                return BadRequest(new { message = "Invalid serial index." });

            Console.WriteLine($"Checking job {request.Job}, operation {request.Operation}, serial {serialNo}");

            // Check current serial
            var currentLatestLog = await _context.EmployeeLog
                .Where(l => l.JobNumber == request.Job
                            && l.OperationNumber == request.Operation
                            && l.serialNo == serialNo)
                .OrderByDescending(l => l.StatusTime)
                .FirstOrDefaultAsync();

            if (currentLatestLog != null)
            {
                Console.WriteLine($"Current serial {serialNo} status: {currentLatestLog.StatusID}");

                if (currentLatestLog.StatusID == 3)
                    return BadRequest(new { message = $"Serial {serialNo} is already completed." });

                if (currentLatestLog.StatusID == 1)
                    return BadRequest(new { message = $"Serial {serialNo} is already started." });

                if (currentLatestLog.StatusID == 2)
                    return Ok(new { allow = true, message = $"Serial {serialNo} can be resumed." });
            }

            // Check previous serials (from 1 to serialIndex-1)
            for (int i = 1; i < serialIndex; i++)
            {
                string prevSerial = $"{baseSerial}_{i}";
                Console.WriteLine(prevSerial);
                var prevLog = await _context.EmployeeLog
                    .Where(l => l.JobNumber == request.Job
                                && l.OperationNumber == request.Operation
                                && l.serialNo == prevSerial)
                    .OrderByDescending(l => l.StatusTime)
                    .FirstOrDefaultAsync();

                Console.WriteLine($"Checking previous serial {prevSerial}, status: {(prevLog?.StatusID.ToString() ?? "null")}");

                if (prevLog == null || prevLog.StatusID != 3)
                {
                    return BadRequest(new { message = $"Cannot start {serialNo} until {prevSerial} is completed." });
                }
            }

            Console.WriteLine($"All previous serials completed. Serial {serialNo} can be started.");
            return Ok(new { allow = true, message = $"Serial {serialNo} can be started." });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
            return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
        }
    }

    


    [HttpPost("JobPoolDetails")]
    public IActionResult JobPoolDetails([FromBody] JobPoolRequest request)
    {
        if (string.IsNullOrEmpty(request.JobPoolNumber))
            return BadRequest("JobPoolNumber is required");

        var data = _context.JobPool
            .Where(j => j.JobPoolNumber == request.JobPoolNumber)
            .OrderByDescending(j => j.Status_Time)
            .Select(j => new
            {
                job = j.Job,
                transactionNum = j.TransactionNum,
                operation = j.Operation,
                workCenter = j.WorkCenter,
                employee = j.Employee,
                machine = j.Machine,
                qty = j.Qty,
                item = j.Item,
                status_ID = j.Status_ID,
                status_Time = j.Status_Time,
                jobPoolNumber = j.JobPoolNumber
            })
            .ToList();

        return Ok(new { total = data.Count, data });
    }



    [HttpPost("jobPoolComplete")]
    public IActionResult JobPoolComplete([FromBody] JobPoolRequest request)
    {
        if (string.IsNullOrEmpty(request.JobPoolNumber))
            return BadRequest("PoolNumber is required.");

        // Fetch all jobs in the pool
        var jobs = _context.JobPool
            .Where(j => j.JobPoolNumber == request.JobPoolNumber)
            .ToList();

        if (!jobs.Any())
            return NotFound("No jobs found for this pool.");

        var now = DateTime.Now;

        var newJobs = new List<JobPool>();

        foreach (var job in jobs)
        {
            // Create a new record based on existing job
            var newJob = new JobPool
            {
                JobPoolNumber = job.JobPoolNumber,
                Job = job.Job,
                TransactionNum = job.TransactionNum,
                Operation = job.Operation,
                WorkCenter = job.WorkCenter,
                Employee = job.Employee,
                Qty = job.Qty,
                Item = job.Item,
                Status_ID = 3,        // Completed
                Status_Time = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            newJobs.Add(newJob);
        }

        _context.JobPool.AddRange(newJobs);
        _context.SaveChanges();

        return Ok(new { message = "Job pool completed successfully (new records inserted).", totalJobs = newJobs.Count });
    }

    [HttpPost("jobPoolHold")]
    public IActionResult jobPoolHold([FromBody] JobPoolRequest request)
    {
        if (string.IsNullOrEmpty(request.JobPoolNumber))
            return BadRequest("PoolNumber is required.");

        // Fetch all jobs in the pool
        var jobs = _context.JobPool
            .Where(j => j.JobPoolNumber == request.JobPoolNumber)
            .ToList();

        if (!jobs.Any())
            return NotFound("No jobs found for this pool.");

        var now = DateTime.Now;

        var newJobs = new List<JobPool>();

        foreach (var job in jobs)
        {
            // Create a new record based on existing job
            var newJob = new JobPool
            {
                JobPoolNumber = job.JobPoolNumber,
                Job = job.Job,
                TransactionNum = job.TransactionNum,
                Operation = job.Operation,
                WorkCenter = job.WorkCenter,
                Employee = job.Employee,
                Qty = job.Qty,
                Item = job.Item,
                Status_ID = 2,        // Completed
                Status_Time = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            newJobs.Add(newJob);
        }

        _context.JobPool.AddRange(newJobs);
        _context.SaveChanges();

        return Ok(new { message = "Job pool completed successfully (new records inserted).", totalJobs = newJobs.Count });
    }

    [HttpPost("jobPoolresume")]
    public IActionResult jobPoolresume([FromBody] JobPoolRequest request)
    {
        if (string.IsNullOrEmpty(request.JobPoolNumber))
            return BadRequest("PoolNumber is required.");

        // Fetch all jobs in the pool
        var jobs = _context.JobPool
            .Where(j => j.JobPoolNumber == request.JobPoolNumber)
            .ToList();

        if (!jobs.Any())
            return NotFound("No jobs found for this pool.");

        var now = DateTime.Now;

        var newJobs = new List<JobPool>();

        foreach (var job in jobs)
        {
            // Create a new record based on existing job
            var newJob = new JobPool
            {
                JobPoolNumber = job.JobPoolNumber,
                Job = job.Job,
                TransactionNum = job.TransactionNum,
                Operation = job.Operation,
                WorkCenter = job.WorkCenter,
                Employee = job.Employee,
                Qty = job.Qty,
                Item = job.Item,
                Status_ID = 1,        // Completed
                Status_Time = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            newJobs.Add(newJob);
        }

        _context.JobPool.AddRange(newJobs);
        _context.SaveChanges();

        return Ok(new { message = "Job pool completed successfully (new records inserted).", totalJobs = newJobs.Count });
    }


    [HttpPost("CreateRolePageMapping")]
    public async Task<IActionResult> CreateRolePageMapping([FromBody] RolePageMapping request)
    {
        request.CreatedAt = DateTime.Now;
        request.UpdatedAt = DateTime.Now;

        _context.RolePageMapping.Add(request);
        await _context.SaveChangesAsync();

        return Ok(new { message = "RolePageMapping created successfully." });
    }

    [HttpPost("GetJobOperationDetails")]
    public async Task<IActionResult> GetJobOperationDetails([FromBody] JobOperationRequest request)
    {
        try
        {
            // Step 1: Released qty from job_mst
            var job = await _context.JobMst
                .Where(j => j.job == request.Job)
                .Select(j => new { j.job, j.qty_released })
                .FirstOrDefaultAsync();

            if (job == null)
                return NotFound(new { message = "Job not found." });

            // Step 2: Work center from jobroute_mst
            var jobRoute = await _context.JobRouteMst
                .Where(r => r.Job == request.Job && r.OperNum == request.OperNum)
                .Select(r => new { r.Job, r.OperNum, r.Wc, r.Suffix })
                .FirstOrDefaultAsync();

            if (jobRoute == null)
                return NotFound(new { message = "Job Route not found." });

            // Step 3: Employees + trans no from jobtran_mst, join with employee_mst
            var employees = await (from jt in _context.JobTranMst
                                   join em in _context.EmployeeMst
                                       on jt.emp_num equals em.emp_num
                                   where jt.job == request.Job && jt.oper_num == request.OperNum

                                   select new EmployeesDto
                                   {
                                       EmpNum = em.emp_num,
                                       Name = em.name,
                                       TransNum = jt.trans_num
                                   }).ToListAsync();

            // Step 4: Build response
            var result = new JobOperationDetailsDtos
            {
                Job = job.job,
                OperNum = request.OperNum,
                ReleasedQty = job.qty_released,
                WorkCenter = jobRoute.Wc,
                suffix = jobRoute.Suffix,
                Employees = employees
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Server error", error = ex.Message });
        }
    }


    [HttpPost("AddMachineEmployee")]
    public async Task<IActionResult> AddMachineEmployee([FromBody] MachineEmployeeDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Invalid data." });

        if (string.IsNullOrWhiteSpace(dto.MachineNumber) || string.IsNullOrWhiteSpace(dto.Emp_Num))
            return BadRequest(new { message = "MachineNumber and Emp_Num are required." });

        // Validation: Check if the same machine-employee pair already exists
        var exists = await _context.WomMachineEmployee
            .FromSqlRaw(@"SELECT * FROM wom_machine_employee 
                        WHERE MachineNumber = {0} AND emp_num = {1}",
                dto.MachineNumber, dto.Emp_Num)
            .AnyAsync();

        if (exists)
            return Conflict(new { message = "This machine is already assigned to the employee." });

        // Insert using parameterized SQL
        string sql = @"
            INSERT INTO wom_machine_employee
                (MachineNumber, MachineDescription, emp_num, name,
                noteexistsflag, recorddate, rowpointer, createdby, updatedby, createdate, inworkflow)
            VALUES
                (@p0, @p1, @p2, @p3,
                @p4, @p5, @p6, @p7, @p8, @p9, @p10)";

        await _context.Database.ExecuteSqlRawAsync(sql,
            dto.MachineNumber,
            dto.MachineDescription ?? string.Empty,
            dto.Emp_Num,
            dto.Name ?? string.Empty,
            0,                // noteexistsflag default
            DateTime.Now,     // recorddate
            Guid.NewGuid(),   // rowpointer
            "system",         // createdby
            "system",         // updatedby
            DateTime.Now,     // createdate
            0                 // inworkflow default
        );

        return Ok(new { message = "Machine employee added successfully." });
    }



    [HttpPost("AddMachineWc")]
    public async Task<IActionResult> AddMachineWc([FromBody] MachineWcDto dto)
    {
        if (dto == null ||
            string.IsNullOrWhiteSpace(dto.wc) ||
            string.IsNullOrWhiteSpace(dto.wcName) ||
            string.IsNullOrWhiteSpace(dto.machineNumber) ||
            string.IsNullOrWhiteSpace(dto.machineDescription))
        {
            return BadRequest(new { message = "All fields are required." });
        }

        var exists = await _context.WomWcMachines
            .AnyAsync(x => x.Wc == dto.wc.Trim() && x.MachineId == dto.machineNumber.Trim());

        if (exists)
            return Conflict(new { message = "This WC is already mapped with this machine." });

        var entity = new WomWcMachine
        {
            RowPointer = Guid.NewGuid(),
            Wc = dto.wc.Trim(),
            WcName = dto.wcName.Trim(),
            MachineId = dto.machineNumber.Trim(),
            MachineDescription = dto.machineDescription.Trim(),
            NoteExistsFlag = 0,
            RecordDate = DateTime.Now,
            CreatedBy = "womme",
            UpdatedBy = "womme",
            CreateDate = DateTime.Now,
            InWorkflow = 0
        };

        // Save in local DB
        _context.WomWcMachines.Add(entity);
        await _context.SaveChangesAsync();

       
        return Ok(new { message = "Machine-WC mapping added successfully to local and Syteline." });
    }


    [HttpPost("StartSingleQCJob")]
    public async Task<IActionResult> StartSingleQCJob([FromBody] StartJobRequestDto jobDto)
    {
        if (jobDto == null)
            return BadRequest("Invalid request");

        try
        {
            // 1️⃣ Fetch the job route details
            var jobRoutes = await (
                from jr in _context.JobRouteMst
                join wm in _context.WcMst on jr.Wc equals wm.wc
                where jr.Job == jobDto.JobNumber
                select new { jr.Job, jr.OperNum, jr.Wc, wm.dept }
            ).OrderBy(x => x.OperNum).ToListAsync();

            if (!jobRoutes.Any())
                return NotFound(new { message = "No job route found for this job." });

            var currentOper = jobRoutes.FirstOrDefault(r => r.OperNum == jobDto.OperationNumber);
            if (currentOper == null)
                return NotFound(new { message = "Operation not found in job route." });

            // ✅ Find previous operation
            var prevOperNum = jobDto.OperationNumber - 10;
            var prevOper = jobRoutes.FirstOrDefault(r => r.OperNum == prevOperNum);

            if (prevOper != null)
            {
                // ✅ Bypass condition: if previous dept == "OPR" and WC contains "issue"
                if (!(prevOper.dept == "OPR" && prevOper.Wc.ToLower().Contains("issue")))
                {
                    // otherwise, check if previous operation completed (status = 3)
                    var prevStatus = await _context.JobTranMst
                        .Where(t => t.job == jobDto.JobNumber && t.oper_num == prevOperNum)
                        .OrderByDescending(t => t.trans_date)
                        .Select(t => t.status)
                        .FirstOrDefaultAsync();

                    if (prevStatus != "3")
                        return BadRequest(new { message = $"Previous operation ({prevOperNum}) not completed. Please complete before starting QC." });
                }
            }
            else
            {
                return BadRequest(new { message = "Previous operation not found — cannot start QC." });
            }

            // ✅ Continue your existing logic from here
            var lastJob = await _context.JobTranMst
                .Where(j => j.job == jobDto.JobNumber
                            && j.oper_num == jobDto.OperationNumber
                            && j.SerialNo == jobDto.SerialNo)
                .OrderByDescending(j => j.trans_date)
                .FirstOrDefaultAsync();

            if (lastJob != null)
            {
                if (lastJob.status == "1")
                    return BadRequest(new { message = "Job is already started." });
                if (lastJob.status == "3")
                    return BadRequest(new { message = "Job is already completed." });

                if (lastJob.status == "2" && lastJob.start_time.HasValue)
                {
                    TimeSpan duration = DateTime.Now - lastJob.start_time.Value;
                    lastJob.a_hrs = (decimal)duration.TotalHours;
                    lastJob.end_time = DateTime.Now;
                    _context.JobTranMst.Update(lastJob);
                }
            }

            // ✅ Get next operation
            var currentIndex = jobRoutes.FindIndex(r => r.OperNum == jobDto.OperationNumber);
            string? nextOperation = (currentIndex + 1 < jobRoutes.Count)
                ? jobRoutes[currentIndex + 1].OperNum.ToString()
                : null;

            // ✅ Employee rate
            var employeeRate = await _context.EmployeeMst
                .Where(e => e.emp_num == jobDto.EmpNum)
                .Select(e => e.mfg_reg_rate)
                .FirstOrDefaultAsync();

            if (employeeRate == 0)
                return NotFound(new { message = "Employee not found or rate = 0." });

            // ✅ Next transaction number
            decimal nextTransNum = (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0) + 1;

            // ✅ QC group logic
            string qcGroupNumber = (lastJob != null && !string.IsNullOrEmpty(lastJob.qcgroup))
                ? lastJob.qcgroup
                : new Random().Next(1000, 9999).ToString();

            // ✅ Create new transaction
            var jobTran = new JobTranMst
            {
                site_ref = "DEFAULT",
                trans_num = nextTransNum,
                job = jobDto.JobNumber,
                SerialNo = jobDto.SerialNo,
                item = jobDto.Item,
                wc = jobDto.Wc,
                emp_num = jobDto.EmpNum,
                qty_complete = 0,
                oper_num = jobDto.OperationNumber,
                next_oper = int.TryParse(nextOperation, out var val) ? val : (int?)null,
                trans_date = DateTime.Now,
                RecordDate = DateTime.Now,
                CreateDate = DateTime.Now,
                CreatedBy = jobDto.loginuser,
                UpdatedBy = jobDto.loginuser,
                completed_flag = false,
                suffix = 0,
                trans_type = "M",
                qty_scrapped = 0,
                qty_moved = 0,
                pay_rate = "R",
                whse = "MAIN",
                close_job = 0,
                issue_parent = 0,
                complete_op = 0,
                shift = "1",
                posted = 1,
                job_rate = employeeRate,
                Uf_MovedOKToStock = 0,
                start_time = DateTime.Now,
                status = "1",
                qcgroup = qcGroupNumber,
                RowPointer = Guid.NewGuid()
            };

            _context.JobTranMst.Add(jobTran);
            await _context.SaveChangesAsync();

           

            return Ok(new
            {
                success = true,
                message = "Single QC job started successfully.",
                qcGroup = qcGroupNumber,
                nextOperation
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while starting job",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }



    [HttpPost("StartGroupQCJobs")]
    public async Task<IActionResult> StartGroupQCJobs([FromBody] StartGroupQCJobRequestDto dto)
    {
        if (dto == null || !dto.Jobs.Any())
            return BadRequest("No jobs provided");

        try
        {
            var qcGroupNumber = new Random().Next(1000, 9999).ToString();
            var addedJobs = new List<decimal>();
            var skippedJobs = new List<object>();

            decimal nextTransNum = (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0) + 1;
            var sytelineService = new SytelineService(_configuration);

            foreach (var jobDto in dto.Jobs)
            {
                try
                {
                    // ✅ Fetch job routes
                    var jobRoutes = await (
                        from jr in _context.JobRouteMst
                        join wm in _context.WcMst on jr.Wc equals wm.wc
                        where jr.Job == jobDto.JobNumber
                        select new { jr.Job, jr.OperNum, jr.Wc, wm.dept }
                    ).OrderBy(x => x.OperNum).ToListAsync();

                    if (!jobRoutes.Any())
                    {
                        skippedJobs.Add(new { jobDto.JobNumber, reason = "No job route found" });
                        continue;
                    }

                    // ✅ Find previous operation
                    var prevOperNum = jobDto.OperationNumber - 10;
                    var prevOper = jobRoutes.FirstOrDefault(r => r.OperNum == prevOperNum);

                    if (prevOper != null)
                    {
                        if (!(prevOper.dept == "OPR" && prevOper.Wc.ToLower().Contains("issue")))
                        {
                            var prevStatus = await _context.JobTranMst
                                .Where(t => t.job == jobDto.JobNumber && t.oper_num == prevOperNum)
                                .OrderByDescending(t => t.trans_date)
                                .Select(t => t.status)
                                .FirstOrDefaultAsync();

                            if (prevStatus != "3")
                            {
                                skippedJobs.Add(new { jobDto.JobNumber, reason = $"Previous operation {prevOperNum} not completed" });
                                continue;
                            }
                        }
                    }
                    else
                    {
                        skippedJobs.Add(new { jobDto.JobNumber, reason = "Previous operation not found" });
                        continue;
                    }

                    // ✅ Check last transaction
                    var lastJob = await _context.JobTranMst
                        .Where(j => j.job == jobDto.JobNumber
                                    && j.oper_num == jobDto.OperationNumber
                                    && j.SerialNo == jobDto.SerialNo)
                        .OrderByDescending(j => j.trans_date)
                        .FirstOrDefaultAsync();

                    if (lastJob != null)
                    {
                        if (lastJob.status == "1")
                        {
                            skippedJobs.Add(new { jobDto.JobNumber, reason = "Already started" });
                            continue;
                        }
                        if (lastJob.status == "3")
                        {
                            skippedJobs.Add(new { jobDto.JobNumber, reason = "Already completed" });
                            continue;
                        }

                        if (lastJob.status == "2" && lastJob.start_time.HasValue)
                        {
                            TimeSpan duration = DateTime.Now - lastJob.start_time.Value;
                            lastJob.a_hrs = (decimal)duration.TotalHours;
                            lastJob.end_time = DateTime.Now;
                            _context.JobTranMst.Update(lastJob);
                        }
                    }

                    // ✅ Next operation
                    var currentIndex = jobRoutes.FindIndex(r => r.OperNum == jobDto.OperationNumber);
                    string? nextOperation = currentIndex + 1 < jobRoutes.Count
                        ? jobRoutes[currentIndex + 1].OperNum.ToString()
                        : null;

                    // ✅ Employee rate
                    var employeeRate = await _context.EmployeeMst
                        .Where(e => e.emp_num == jobDto.EmpNum)
                        .Select(e => e.mfg_reg_rate)
                        .FirstOrDefaultAsync();

                    if (employeeRate == 0)
                    {
                        skippedJobs.Add(new { jobDto.JobNumber, reason = $"Employee {jobDto.EmpNum} not found or rate = 0" });
                        continue;
                    }

                    // ✅ Create new transaction
                    var jobTran = new JobTranMst
                    {
                        site_ref = "DEFAULT",
                        trans_num = nextTransNum,
                        job = jobDto.JobNumber,
                        SerialNo = jobDto.SerialNo,
                        item = jobDto.Item,
                        wc = jobDto.Wc,
                        emp_num = jobDto.EmpNum,
                        qty_complete = 0,
                        oper_num = jobDto.OperationNumber,
                        next_oper = int.TryParse(nextOperation, out var val) ? val : (int?)null,
                        trans_date = DateTime.Now,
                        RecordDate = DateTime.Now,
                        CreateDate = DateTime.Now,
                        CreatedBy = jobDto.loginuser,
                        UpdatedBy = jobDto.loginuser,
                        completed_flag = false,
                        suffix = 0,
                        trans_type = "M",
                        qty_scrapped = 0,
                        qty_moved = 0,
                        pay_rate = "R",
                        whse = "MAIN",
                        close_job = 0,
                        issue_parent = 0,
                        complete_op = 0,
                        shift = "1",
                        posted = 1,
                        job_rate = employeeRate,
                        Uf_MovedOKToStock = 0,
                        start_time = DateTime.Now,
                        status = "1",
                        qcgroup = qcGroupNumber,
                        RowPointer = Guid.NewGuid()
                    };

                    _context.JobTranMst.Add(jobTran);
                    addedJobs.Add(nextTransNum);

                    

                    nextTransNum++;
                }
                catch (Exception innerEx)
                {
                    skippedJobs.Add(new { jobDto.JobNumber, reason = $"Error: {innerEx.Message}" });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                qcGroup = qcGroupNumber,
                startedJobs = addedJobs,
                skippedJobs
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while starting group QC jobs",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }



    [HttpPost("PauseSingleQCJob")]
    public async Task<IActionResult> PauseSingleQCJob([FromBody] StartJobRequestDto dto)
    {
        if (dto == null)
            return BadRequest("Invalid request");

        try
        {
            // 🔹 Get last active job (status = 1)
            var lastJob = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo
                            && j.status == "1")
                .OrderByDescending(j => j.trans_date)
                .FirstOrDefaultAsync();

            if (lastJob == null)
                return BadRequest(new { message = "No active job found to pause." });

            var now = DateTime.Now;
            var startTime = lastJob.start_time ?? lastJob.trans_date ?? now;

            // 🔹 Calculate worked hours and amount
            TimeSpan duration = now - startTime;
            decimal totalHoursDecimal = (decimal)duration.TotalHours;

            var jobRate = await _context.EmployeeMst
                .Where(e => e.emp_num == dto.EmpNum)
                .Select(e => e.mfg_reg_rate)
                .FirstOrDefaultAsync() ?? 0m;

            decimal a_dollar = totalHoursDecimal * jobRate;

            // 🔹 Update last running row
            lastJob.a_hrs = totalHoursDecimal;
            lastJob.a_dollar = a_dollar;
            lastJob.end_time = now;
            lastJob.job_rate = jobRate;
            _context.JobTranMst.Update(lastJob);

            // 🔹 Insert paused marker row
            decimal nextTransNum = (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0) + 1;

            var pausedJob = new JobTranMst
            {
                site_ref = "DEFAULT",
                trans_num = nextTransNum,
                job = lastJob.job,
                SerialNo = lastJob.SerialNo,
                item = lastJob.item,
                wc = lastJob.wc,
                emp_num = lastJob.emp_num,
                qty_complete = lastJob.qty_complete,
                oper_num = lastJob.oper_num,
                next_oper = lastJob.next_oper,
                trans_date = now,
                RecordDate = now,
                CreateDate = now,
                CreatedBy = dto.loginuser,
                UpdatedBy = dto.loginuser,
                completed_flag = false,
                suffix = lastJob.suffix,
                trans_type = "M",
                qty_scrapped = lastJob.qty_scrapped,
                qty_moved = lastJob.qty_moved,
                pay_rate = lastJob.pay_rate,
                whse = lastJob.whse,
                close_job = lastJob.close_job,
                issue_parent = lastJob.issue_parent,
                complete_op = lastJob.complete_op,
                shift = lastJob.shift,
                posted = lastJob.posted,
                Uf_MovedOKToStock = lastJob.Uf_MovedOKToStock,
                start_time = now, // start time for next resume
                end_time = null,
                a_hrs = 0,
                a_dollar = 0,
                status = "2",
                qcgroup = lastJob.qcgroup,
                RowPointer = Guid.NewGuid()
            };

            _context.JobTranMst.Add(pausedJob);

            // Save all changes
            await _context.SaveChangesAsync();

            // Get last 2 rows
            var lastTwoRows = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                        && j.oper_num == dto.OperationNumber
                        && j.wc == dto.Wc
                        && j.SerialNo == dto.SerialNo)
                .OrderByDescending(j => j.trans_num)
                .Take(2)
                .ToListAsync();

            var latestRow = lastTwoRows.FirstOrDefault();               // status 2
            var secondLastRow = lastTwoRows.Skip(1).FirstOrDefault();   // status 1

            // Check your condition
            if (latestRow?.status == "2" && secondLastRow?.status == "1")
            {
                var sytelineTransNum = await _sytelineService.InsertJobTranAsync(secondLastRow,0);

                if (sytelineTransNum != null)
                {
                    secondLastRow.import_doc_id = sytelineTransNum.Value.ToString();
                    _context.JobTranMst.Update(secondLastRow);
                    await _context.SaveChangesAsync();
                }
            }
           

            return Ok(new
            {
                success = true,
                message = "Single QC job paused successfully.",
                hoursWorked = totalHoursDecimal,
                amount = a_dollar
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while pausing job",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }

    //CompleteJob
    [HttpPost("CompleteSingleQCJob")]
    public async Task<IActionResult> CompleteSingleQCJob([FromBody] StartJobRequestDto dto)
    {
        if (dto == null)
            return BadRequest("Invalid request");

        try
        {
            var now = DateTime.Now;

            // 🔹 Get last job (running or paused)
            var latestJob = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                         && j.oper_num == dto.OperationNumber
                         && j.wc == dto.Wc
                         && j.SerialNo == dto.SerialNo)
                .OrderByDescending(j => j.trans_date)
                .FirstOrDefaultAsync();

            if (latestJob == null)
                return NotFound(new { message = "No job transactions found." });

            if (latestJob.status == "3")
                return BadRequest(new { message = "Job is already completed." });

            var empRates = await _context.EmployeeMst
                .Where(e => e.emp_num == dto.EmpNum)
                .Select(e => new { RegRate = e.mfg_reg_rate, OtRate = e.mfg_ot_rate })
                .FirstOrDefaultAsync();

            if (empRates == null || empRates.RegRate == 0)
                return NotFound(new { message = "Employee not found or invalid rate." });

            decimal regRate = empRates.RegRate ?? 0m;
            decimal otRate = empRates.OtRate ?? 0m;
            // 🔹 Close last job (running or paused)
            if (latestJob.status == "2") // paused
            {
                if (!latestJob.end_time.HasValue)
                    latestJob.end_time = now;

                latestJob.a_hrs = (decimal)(latestJob.end_time.Value - latestJob.start_time.Value).TotalHours;
                latestJob.a_dollar = latestJob.a_hrs * regRate; // paused row not included in totalHours
                latestJob.UpdatedBy = dto.loginuser;

                _context.JobTranMst.Update(latestJob);
                await _context.SaveChangesAsync();
            }
            else if (latestJob.status == "1") // running
            {
                latestJob.end_time = now;
                latestJob.a_hrs = (decimal)(latestJob.end_time.Value - latestJob.start_time.Value).TotalHours;
                latestJob.a_dollar = latestJob.a_hrs * regRate;

                _context.JobTranMst.Update(latestJob);
                await _context.SaveChangesAsync();
            }

            // 🔹 Determine first start time (always first running row in same qc group)
            DateTime firstStartTime;

            var firstRunningRow = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                         && j.oper_num == dto.OperationNumber
                         && j.wc == dto.Wc
                         && j.SerialNo == dto.SerialNo
                         && j.qcgroup == latestJob.qcgroup
                )
                .OrderBy(j => j.start_time)
                .FirstOrDefaultAsync();

            firstStartTime = firstRunningRow?.start_time ?? now;

            // 🔹 Calculate totalHours from status=1 rows only
            var runningRows = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                         && j.oper_num == dto.OperationNumber
                         && j.wc == dto.Wc
                         && j.SerialNo == dto.SerialNo
                         && j.status == "1"
                         && j.a_hrs > 0)
                .ToListAsync();

            decimal totalHours = runningRows.Sum(r => r.a_hrs ?? 0m);
            if (totalHours <= 0) totalHours = 0.01m;

            decimal rateToUse = totalHours <= 8 ? regRate : otRate;
            string shiftToUse = totalHours <= 8 ? "1" : "2";



            // 🔹 Insert completion row
            decimal nextTransNum = (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0) + 1;

            var completedJob = new JobTranMst
            {
                site_ref = "DEFAULT",
                trans_num = nextTransNum,
                job = latestJob.job,
                SerialNo = latestJob.SerialNo,
                item = latestJob.item,
                wc = latestJob.wc,
                emp_num = latestJob.emp_num,
                qty_complete = 1,
                oper_num = latestJob.oper_num,
                next_oper = latestJob.next_oper,
                trans_date = now,
                RecordDate = now,
                CreateDate = now,
                CreatedBy = dto.loginuser,
                UpdatedBy = dto.loginuser,
                completed_flag = true,
                suffix = 0,
                trans_type = "M",
                qty_scrapped = 0,
                qty_moved = 0,
                pay_rate = "R",
                whse = "MAIN",
                close_job = 1,
                issue_parent = 0,
                complete_op = 1,
                shift = shiftToUse,
                posted = 1,
                job_rate = rateToUse,
                Uf_MovedOKToStock = 0,
                start_time = firstStartTime,
                end_time = now,
                status = "3",
                a_hrs = totalHours,
                a_dollar = totalHours * rateToUse,
                RowPointer = Guid.NewGuid(),
                qcgroup = latestJob.qcgroup
            };

            _context.JobTranMst.Add(completedJob);
            await _context.SaveChangesAsync();

           // Get last 2 rows
            var lastTwoRows = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                        && j.oper_num == dto.OperationNumber
                        && j.wc == dto.Wc
                        && j.SerialNo == dto.SerialNo)
                .OrderByDescending(j => j.trans_num)
                .Take(2)
                .ToListAsync();

            var latestRow = lastTwoRows.FirstOrDefault();               // status 3
            var secondLastRow = lastTwoRows.Skip(1).FirstOrDefault();   // status 1

            // Check your condition
            if (latestRow?.status == "3" && secondLastRow?.status == "1")
            {
                var sytelineTransNum = await _sytelineService.InsertJobTranAsync(secondLastRow, 1);

                if (sytelineTransNum != null)
                {
                    secondLastRow.import_doc_id = sytelineTransNum.Value.ToString();
                    _context.JobTranMst.Update(secondLastRow);
                    await _context.SaveChangesAsync();
                }
            }

            // CASE 2: latestRow = 3, secondLastRow = 2
            if (latestRow?.status == "3" && secondLastRow?.status == "2")
            {
                // Find the 3rd last row which MUST be status = 1
                var thirdLastRow = await _context.JobTranMst
                    .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo
                            && j.status == "1")
                    .OrderByDescending(j => j.trans_num)
                    .FirstOrDefaultAsync();

                if (thirdLastRow == null)
                {
                    Console.WriteLine("No third last running row found with status 1.");
                }
                else
                {
                    Console.WriteLine($"ThirdLastRow Found => trans_num={thirdLastRow.trans_num}, import_doc_id={thirdLastRow.import_doc_id}");

                    // import_doc_id is the Syteline trans number
                    if (!string.IsNullOrEmpty(thirdLastRow.import_doc_id))
                    {
                        int sytelineTransnumber = int.Parse(thirdLastRow.import_doc_id);

                        // Call SYTELINE to update the row as completed
                        var updateResult = await _sytelineService.UpdateJobTranCompletionAsync(sytelineTransnumber, 1);

                        if (updateResult)
                        {
                            Console.WriteLine("Syteline row updated to complete=1 successfully.");
                        }
                        else
                        {
                            Console.WriteLine("Failed to update Syteline row.");
                        }
                    }
                }
            }



            return Ok(new
            {
                success = true,
                message = "Single QC job completed successfully.",
                hoursWorked = totalHours,
                amount = totalHours * rateToUse,
                transNum = nextTransNum
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while completing job",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }


    [HttpPost("PauseGroupQCJobs")]
    public async Task<IActionResult> PauseGroupQCJobs([FromBody] StartGroupQCJobRequestDto dto)
    {
        if (dto == null || !dto.Jobs.Any())
            return BadRequest("No jobs provided");

        try
        {
            var pausedJobs = new List<decimal>();

            // Get the current max trans_num ONCE to avoid duplicate keys
            decimal currentMaxTransNum = (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0);

            foreach (var jobDto in dto.Jobs)
            {
                // fetch without tracking to avoid EF conflicts
                var lastJob = await _context.JobTranMst
                    .AsNoTracking()
                    .Where(j => j.job == jobDto.JobNumber
                                && j.oper_num == jobDto.OperationNumber
                                && j.SerialNo == jobDto.SerialNo)
                    .OrderByDescending(j => j.trans_date)
                    .FirstOrDefaultAsync();

                if (lastJob == null)
                    continue;

                if (lastJob.status != "1") // only active jobs can be paused
                    continue;

                var now = DateTime.Now;
                var startTime = lastJob.start_time ?? lastJob.trans_date ?? now;
                TimeSpan duration = now - startTime;
                decimal totalHoursDecimal = (decimal)duration.TotalHours;

                var jobRateNullable = await _context.EmployeeMst
                    .Where(e => e.emp_num == jobDto.EmpNum)
                    .Select(e => e.mfg_reg_rate)
                    .FirstOrDefaultAsync();

                decimal jobRate = jobRateNullable ?? 0m;
                decimal a_dollar = totalHoursDecimal * jobRate;

                // increment trans_num safely
                currentMaxTransNum++;
                decimal nextTransNum = currentMaxTransNum;

                // new paused record
                var pausedJob = new JobTranMst
                {
                    site_ref = "DEFAULT",
                    trans_num = nextTransNum,
                    job = lastJob.job,
                    SerialNo = lastJob.SerialNo,
                    item = lastJob.item ?? jobDto.Item, // ensure item is saved
                    wc = lastJob.wc,
                    emp_num = lastJob.emp_num,
                    qty_complete = lastJob.qty_complete,
                    oper_num = lastJob.oper_num,
                    next_oper = lastJob.next_oper,
                    trans_date = now,
                    RecordDate = now,
                    CreateDate = now,
                    CreatedBy = jobDto.loginuser,
                    UpdatedBy = jobDto.loginuser,
                    completed_flag = lastJob.completed_flag,
                    suffix = lastJob.suffix,
                    trans_type = lastJob.trans_type,
                    qty_scrapped = lastJob.qty_scrapped,
                    qty_moved = lastJob.qty_moved,
                    pay_rate = lastJob.pay_rate,
                    whse = lastJob.whse,
                    close_job = lastJob.close_job,
                    issue_parent = lastJob.issue_parent,
                    complete_op = lastJob.complete_op,
                    shift = lastJob.shift,
                    posted = lastJob.posted,
                    Uf_MovedOKToStock = lastJob.Uf_MovedOKToStock,
                    start_time = now,
                    status = "2", // paused
                    qcgroup = lastJob.qcgroup!.ToString(),  // 👈 ensure group ID carried
                    RowPointer = Guid.NewGuid()
                };

                // add paused record
                _context.JobTranMst.Add(pausedJob);

                // update the *previous* active job’s end time + cost
                lastJob.a_hrs = totalHoursDecimal;
                lastJob.a_dollar = a_dollar;
                lastJob.end_time = now;
                lastJob.job_rate = jobRate;

                // attach lastJob for update
                _context.JobTranMst.Attach(lastJob);
                _context.Entry(lastJob).State = EntityState.Modified;

                pausedJobs.Add(nextTransNum);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Group QC jobs paused successfully.",
                transNumbers = pausedJobs
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while pausing group QC jobs",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }


    //CompleteJob
    [HttpPost("CompleteGroupQCJobs")]
    public async Task<IActionResult> CompleteGroupQCJobs([FromBody] StartGroupQCJobRequestDto dto)
    {
        if (dto == null || !dto.Jobs.Any())
            return BadRequest("No jobs provided");

        try
        {
            var now = DateTime.Now;
            var addedJobs = new List<decimal>();

            // ✅ Get last used trans_num once
            decimal nextTransNum = (await _context.JobTranMst.MaxAsync(j => (decimal?)j.trans_num)) ?? 0;

            foreach (var jobDto in dto.Jobs)
            {
                var latestJob = await _context.JobTranMst
                    .Where(j => j.job == jobDto.JobNumber
                             && j.oper_num == jobDto.OperationNumber
                             && j.item == jobDto.Item
                             && j.SerialNo == jobDto.SerialNo)
                    .OrderByDescending(j => j.trans_date)
                    .FirstOrDefaultAsync();

                if (latestJob == null || latestJob.status == "3")
                    continue;

                var empRates = await _context.EmployeeMst
                    .Where(e => e.emp_num == jobDto.EmpNum)
                    .Select(e => new { RegRate = e.mfg_reg_rate, OtRate = e.mfg_ot_rate })
                    .FirstOrDefaultAsync();

                if (empRates == null || empRates.RegRate == 0)
                    continue;

                decimal regRate = empRates.RegRate ?? 0m;
                decimal otRate = empRates.OtRate ?? 0m;

                DateTime startFrom = latestJob.status == "2"
                    ? latestJob.end_time ?? latestJob.trans_date ?? now
                    : latestJob.start_time ?? latestJob.trans_date ?? now;

                decimal totalHours = (decimal)(now - startFrom).TotalHours;
                decimal remaining = totalHours;

                // ✅ Close paused row if any
                if (latestJob.status == "2")
                {
                    latestJob.end_time = now;
                    _context.Entry(latestJob).State = EntityState.Modified;
                }

                decimal hoursSoFar = 0;

                while (remaining > 0)
                {
                    decimal thisChunk = remaining > 8 ? 8 : remaining;
                    decimal rateToUse = (hoursSoFar < 8) ? regRate : otRate;
                    string shiftToUse = (hoursSoFar < 8) ? "1" : "2";

                    nextTransNum++; // ✅ increment first

                    var completedJob = new JobTranMst
                    {
                        site_ref = "DEFAULT",
                        trans_num = nextTransNum, // ✅ always unique
                        job = latestJob.job,
                        SerialNo = latestJob.SerialNo,
                        item = latestJob.item,
                        emp_num = latestJob.emp_num,
                        qty_complete = 1,
                        oper_num = latestJob.oper_num,
                        next_oper = latestJob.next_oper,
                        trans_date = now,
                        RecordDate = now,
                        CreateDate = now,
                        CreatedBy = jobDto.loginuser,
                        UpdatedBy = jobDto.loginuser,
                        completed_flag = true,
                        suffix = 0,
                        trans_type = "M",
                        qty_scrapped = 0,
                        qty_moved = 0,
                        pay_rate = "R",
                        whse = "MAIN",
                        close_job = 1,
                        issue_parent = 0,
                        complete_op = 1,
                        shift = shiftToUse,
                        posted = 1,
                        job_rate = rateToUse,
                        Uf_MovedOKToStock = 0,
                        start_time = startFrom,
                        end_time = now,
                        status = "3",
                        a_hrs = thisChunk,
                        a_dollar = thisChunk * rateToUse,
                        RowPointer = Guid.NewGuid(),
                        qcgroup = latestJob.qcgroup
                    };

                    await _context.JobTranMst.AddAsync(completedJob);
                    addedJobs.Add(nextTransNum);

                    hoursSoFar += thisChunk;
                    remaining -= thisChunk;
                }
            }

            await _context.SaveChangesAsync();

             var rowsToPush = new List<JobTranMst>();

            foreach (var j in dto.Jobs)
            {
                var groupRows = await _context.JobTranMst
                    .Where(x => x.job == j.JobNumber
                            && x.oper_num == j.OperationNumber
                            && x.wc == j.Wc
                            && x.SerialNo == j.SerialNo
                            && x.status == "1")
                    .OrderBy(x => x.start_time)
                    .ToListAsync();

                rowsToPush.AddRange(groupRows);
            }

            await _sytelineService.InsertJobTransBulkAsync(rowsToPush);


            return Ok(new
            {
                success = true,
                message = "Group QC jobs completed successfully.",
                transNumbers = addedJobs
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while completing group QC jobs",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }


    [HttpPost("AddEmployeeWc")]
    public async Task<IActionResult> AddEmployeeWc([FromBody] EmployeeWcDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Wc) || string.IsNullOrWhiteSpace(dto.EmpNum))
        {
            return BadRequest(new { message = "WC code and Employee number are required." });
        }

        // Check if mapping already exists
        var exists = await _context.WomWcEmployee
            .AnyAsync(x => x.Wc == dto.Wc.Trim() && x.EmpNum == dto.EmpNum.Trim());

        if (exists)
            return Conflict(new { message = "This WC is already mapped with this employee." });

        var entity = new WomWcEmployee
        {
            RowPointer = Guid.NewGuid(),
            Wc = dto.Wc.Trim(),
            EmpNum = dto.EmpNum.Trim(),
            Description = dto.Description?.Trim(),
            Name = dto.Name?.Trim(),
            NoteExistsFlag = 0,
            RecordDate = DateTime.Now,
            CreatedBy = "womme",
            UpdatedBy = "womme",
            CreateDate = DateTime.Now,
            InWorkflow = 0
        };

        // ✅ Insert into local DB
        _context.WomWcEmployee.Add(entity);
        await _context.SaveChangesAsync();

        

        return Ok(new { message = "Employee-WC mapping added successfully to local and Syteline." });
    }

    

    [HttpPost("UpdateQCRemark")]
    public async Task<IActionResult> UpdateQCRemark([FromBody] QCRemarkUpdateDto dto)
    {
        if (dto == null || dto.trans_num == 0)
        {
            return BadRequest(new { message = "Invalid transaction details." });
        }

        string connectionString = _configuration.GetConnectionString("DefaultConnection")!;

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            string query = @"
            UPDATE jobtran_mst 
            SET Remark = @Remark
            WHERE trans_num = @TransNo;
        ";

            using (SqlCommand cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@Remark", dto.Remark ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@TransNo", dto.trans_num);

                await connection.OpenAsync();
                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                    return BadRequest(new { message = "No record found for the given transaction number." });
            }
        }

        return Ok(new { message = "Remark updated successfully." });
    }

    [HttpPost("GetTransactionOverview")]
    public async Task<IActionResult> GetTransactionOverview([FromBody] TransactionOverviewRequest request)
    {
        try
        {
            var today = DateTime.Now.Date;

            bool todayOnly = request.todayOnly == 1;
            bool includeTransaction = request.includeTransaction == 1;
            bool includeQC = request.includeQC == 1;

            var allTrans = _context.JobTranMst.AsQueryable();

            List<JobTranMst> latestNormal = new();
            if (includeTransaction)
            {
                var normalTrans = allTrans.Where(j => j.trans_type != "M");

                if (todayOnly)
                    normalTrans = normalTrans.Where(j => j.trans_date.HasValue && j.trans_date.Value.Date == today);

                latestNormal = await normalTrans
                    .GroupBy(j => j.job)
                    .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault()!)
                    .ToListAsync();
            }

            List<JobTranMst> latestQC = new();
            if (includeQC)
            {
                var qcTrans = allTrans.Where(j => j.trans_type == "M");

                if (todayOnly)
                    qcTrans = qcTrans.Where(j => j.trans_date.HasValue && j.trans_date.Value.Date == today);

                latestQC = await qcTrans
                    .GroupBy(j => j.job)
                    .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault()!)
                    .ToListAsync();
            }

            var transactionOverview = includeTransaction
                ? new
                {
                    RunningJobs = latestNormal.Count(j => j.status == "1"),
                    PausedJobs = latestNormal.Count(j => j.status == "2"),
                    ExtendedJobs = latestNormal.Count(j => j.trans_type == "O"),
                    CompletedJobs = latestNormal.Count(j => j.status == "3" && j.completed_flag == true)
                }
                : null;

            var qcOverview = includeQC
                ? new
                {
                    RunningQCJobs = latestQC.Count(j => j.status == "1"),
                    PausedQCJobs = latestQC.Count(j => j.status == "2"),
                    ExtendedQCJobs = latestQC.Count(j => j.trans_type == "O"),
                    CompletedQCJobs = latestQC.Count(j => j.status == "3" && j.completed_flag == true)
                }
                : null;

            return Ok(new
            {
                success = true,
                todayOnly,
                includeTransaction,
                includeQC,
                TransactionOverview = transactionOverview,
                QCOverview = qcOverview
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while fetching transaction overview.",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }

    [HttpPost("GetTransactionData")]
    public async Task<IActionResult> GetTransactionData([FromBody] JobOverviewFilterDto filter)
    {
        try
        {
            var today = DateTime.Now.Date;
            bool todayOnly = filter.TodayOnly == 1;
            bool includeTransaction = filter.IncludeTransaction == 1;
            bool includeQC = filter.IncludeQC == 1;

            int pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            int pageSize = filter.PageSize <= 0 ? 50 : filter.PageSize;

            var allTransQuery = _context.JobTranMst.AsQueryable();

            if (todayOnly)
                allTransQuery = allTransQuery.Where(j => j.trans_date.HasValue && j.trans_date.Value.Date == today);

            List<JobTranMst> latestNormal = new();
            List<JobTranMst> latestQC = new();

            // ✅ Fetch latest Transaction Jobs safely (in-memory grouping)
            if (includeTransaction)
            {
                var normalData = await allTransQuery
                    .Where(j => j.trans_type != "M")
                    .ToListAsync();

                latestNormal = normalData
                    .GroupBy(j => new { j.job, j.SerialNo, j.wc })
                    .Select(g => g.OrderByDescending(x => x.trans_date).First())
                    .ToList();
            }

            // ✅ Fetch latest QC Jobs safely (in-memory grouping)
            if (includeQC)
            {
                var qcData = await allTransQuery
                    .Where(j => j.trans_type == "M")
                    .ToListAsync();

                latestQC = qcData
                    .GroupBy(j => new { j.job, j.SerialNo, j.wc })
                    .Select(g => g.OrderByDescending(x => x.trans_date).First())
                    .ToList();
            }

            // ✅ Merge both and format
            var combined = latestNormal.Concat(latestQC)
                .Select(j => new
                {
                    Job = j.job,
                    SerialNo = j.SerialNo,
                    WC = j.wc,
                    Operation = j.oper_num,
                    Employee = j.emp_num,
                    Machine = j.machine_id,
                    Time = j.trans_date,
                    Progress = j.status switch
                    {
                        "1" => "Running",
                        "2" => "Paused",
                        "3" => "Completed",
                        _ => "Unknown"
                    },
                    Type = j.trans_type == "M" ? "QC" : "Transaction",
                    QcGroup = j.qcgroup
                })
                .OrderByDescending(j => j.Time)
                .ToList();

            var totalRecords = combined.Count;

            // ✅ Apply Pagination
            var pagedData = combined
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                success = true,
                todayOnly,
                includeTransaction,
                includeQC,
                totalRecords,
                pageNumber,
                pageSize,
                totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                data = pagedData
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error fetching transaction data.",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }



    [HttpPost("MarkJobScrapped")]
    public async Task<IActionResult> MarkJobScrapped([FromBody] ScrapJobDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.SerialNo))
            return BadRequest("Invalid request data.");

        try
        {
            var job = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                        && j.SerialNo == dto.SerialNo
                        && j.oper_num == dto.OperationNumber
                        && j.status == "3") // Only completed
                .OrderByDescending(j => j.trans_date)
                .FirstOrDefaultAsync();

            if (job == null)
                return NotFound(new { message = "Completed job not found for scrapping." });

            job.qty_scrapped = 1;
            job.UpdatedBy = dto.LoginUser;
            job.RecordDate = DateTime.Now;

            _context.JobTranMst.Update(job);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Job marked as scrapped successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while marking job as scrapped.",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }


}

