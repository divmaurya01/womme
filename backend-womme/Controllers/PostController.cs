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
                _context.Entry(secondLastRow).State = EntityState.Detached;                    
                    secondLastRow.qcgroup = "";

                var sytelineTransNum = await _sytelineService.InsertJobTranAsync(secondLastRow,0);

                if (sytelineTransNum != null)
                {
                    latestRow.import_doc_id = sytelineTransNum.Value.ToString();
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

                    // Complete job flag if all opr done
                    var routeOperations = await _context.JobRouteMst
                    .Where(r => r.Job == dto.JobNumber)
                    .Select(r => r.OperNum)
                    .Distinct()
                    .ToListAsync();

                    var completedOperations = await _context.JobTranMst
                    .Where(t => t.job == dto.JobNumber
                            && t.status == "3")
                    .Select(t => t.oper_num)
                    .Distinct()
                    .ToListAsync();

                    bool allOperationsCompleted = routeOperations
                        .All(op => completedOperations.Contains(op));

                    byte closeJobFlag = (byte)(allOperationsCompleted ? 1 : 0);

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
                        trans_type = "D",
                        qty_scrapped = lastJob.qty_scrapped,
                        qty_moved = 1,
                        pay_rate = lastJob.pay_rate,
                        whse = lastJob.whse,
                        close_job = closeJobFlag,
                        issue_parent = lastJob.issue_parent,
                        complete_op = 1,
                        shift = "1",
                        posted = 1,
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
                        qcgroup = ""
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
                        _context.Entry(secondLastRow).State = EntityState.Detached;
                            // Explicitly override only required values
                            secondLastRow.qty_complete = 1;
                            secondLastRow.qty_moved = 1;
                            secondLastRow.qcgroup = "";
                            secondLastRow.close_job = (latestRow?.close_job == 1) ? (byte)1 : (byte)0;
                        var sytelineTransNum = await _sytelineService.InsertJobTranAsync(secondLastRow, 1);

                        if (sytelineTransNum != null)
                        {
                            latestRow.import_doc_id = sytelineTransNum.Value.ToString();
                            _context.JobTranMst.Update(secondLastRow);
                            await _context.SaveChangesAsync();
                        }
                    }

                    // CASE 2: latestRow = 3, secondLastRow = 2
                    if (latestRow?.status == "3" && secondLastRow?.status == "2")
                    {
                        var lastImportDocId = await _context.JobTranMst
                        .Where(j => j.job == dto.JobNumber
                                    && j.oper_num == dto.OperationNumber
                                    && j.wc == dto.Wc
                                    && j.SerialNo == dto.SerialNo
                                    && !string.IsNullOrEmpty(j.import_doc_id))
                        .OrderByDescending(j => j.trans_num)
                        .Select(j => j.import_doc_id)
                        .FirstOrDefaultAsync();

                    if (string.IsNullOrEmpty(lastImportDocId))
                        return BadRequest(new { message = "No Syteline transaction number found to update." });
                        
                        bool sytelineResult = await _sytelineService.UpdateJobTranCompletionAsync(
                            Convert.ToInt32(lastImportDocId),               // trans_num in Syteline
                            (byte)latestRow.complete_op,                          // byte
                            latestRow.close_job ?? 0,                       // byte
                            latestRow.qty_complete ?? 0,
                            latestRow.qty_moved ?? 0
                        );
                        
                    }

            
            // OT logic starts
            Console.WriteLine($"Starting OT logic here ..");

              var runningJobs = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo
                            && j.status == "1")
                .OrderBy(j => j.trans_date)
                .ToListAsync();

            decimal totalAhrs = runningJobs.Sum(j => j.a_hrs ?? 0m);

            if (totalAhrs > 8m)
            {
                var firstRow = runningJobs.FirstOrDefault();
                if (firstRow != null && firstRow.start_time.HasValue)
                {
                    decimal extraHours = totalAhrs - 8m;

                    // Pull relevant data from first row
                    int? nextOper = firstRow.next_oper;
                    string? lastQcGroup = firstRow.qcgroup;
                    string? lastTransType = firstRow.trans_type;

                    // Remove old running rows
                    _context.JobTranMst.RemoveRange(runningJobs);
                    await _context.SaveChangesAsync();

                    totalHours = totalAhrs;

                    decimal transNum = (_context.JobTranMst.Max(x => (decimal?)x.trans_num) ?? 0) + 1;

                    // Get employee REG / OT rate
                    empRates = await _context.EmployeeMst
                        .Where(e => e.emp_num == dto.EmpNum)
                        .Select(e => new { RegRate = e.mfg_reg_rate, OtRate = e.mfg_ot_rate })
                        .FirstOrDefaultAsync();

                    regRate = empRates?.RegRate ?? 0m;
                    otRate = empRates?.OtRate ?? regRate;

                    // Check if all operations completed
                    var routeOps = await _context.JobRouteMst
                        .Where(r => r.Job == dto.JobNumber)
                        .Select(r => r.OperNum)
                        .Distinct()
                        .ToListAsync();

                    var completedOps = await _context.JobTranMst
                        .Where(t => t.job == dto.JobNumber && t.status == "3")
                        .Select(t => t.oper_num)
                        .Distinct()
                        .ToListAsync();

                    bool allOpsCompleted = routeOps.All(op => completedOps.Contains(op));
                    closeJobFlag = (byte)(allOpsCompleted ? 1 : 0);

                    // Build new JobTran rows using firstRow as base
                    var jobTranRows = new List<JobTranMst>();
                    DateTime currentStartTime = firstRow.start_time.Value;
                    decimal remainingHours = totalHours;
                    decimal currentTransNum = transNum;

                    while (remainingHours > 0)
                    {
                        decimal hrsForThisRow = remainingHours >= 8 ? 8 : remainingHours;
                        bool isRegular = currentTransNum == transNum;

                        DateTime currentEndTime = currentStartTime.AddHours((double)hrsForThisRow);
                        decimal rate = isRegular ? regRate : otRate;

                        jobTranRows.Add(new JobTranMst
                        {
                            // Inherit from firstRow
                            site_ref = firstRow.site_ref,
                            suffix = firstRow.suffix,
                            machine_id = firstRow.machine_id,
                            emp_num = firstRow.emp_num,
                            qcgroup = lastQcGroup,
                            trans_class = firstRow.trans_class,
                            item = firstRow.item,
                            whse = firstRow.whse,
                            shift = firstRow.shift,
                            posted = firstRow.posted,
                            issue_parent = firstRow.issue_parent,

                            // Use DTO for the 4 key fields
                            job = dto.JobNumber,
                            oper_num = dto.OperationNumber,
                            wc = dto.Wc,
                            SerialNo = dto.SerialNo,

                            next_oper = nextOper,
                            trans_type = lastTransType,

                            // OT / calculated values
                            trans_num = currentTransNum,
                            qty_complete = 0,
                            qty_scrapped = 0,
                            qty_moved = 0,
                            close_job = 0,
                            complete_op = 0,
                            completed_flag = false,
                            pay_rate = isRegular ? "R" : "O",
                            job_rate = rate,
                            a_hrs = hrsForThisRow,
                            a_dollar = hrsForThisRow * rate,
                            start_time = currentStartTime,
                            end_time = currentEndTime,

                            status = "1",
                            trans_date = DateTime.Now,
                            RecordDate = DateTime.Now,
                            CreateDate = DateTime.Now,
                            CreatedBy = firstRow.CreatedBy,
                            UpdatedBy = firstRow.UpdatedBy,
                            RowPointer = Guid.NewGuid()
                        });

                        remainingHours -= hrsForThisRow;
                        currentTransNum++;
                        currentStartTime = currentEndTime;   // move clock forward
                    }

                    // Final completed row
                    jobTranRows.Add(new JobTranMst
                    {
                        site_ref = firstRow.site_ref,
                        suffix = firstRow.suffix,
                        machine_id = firstRow.machine_id,
                        emp_num = firstRow.emp_num,
                        qcgroup = lastQcGroup,
                        trans_class = firstRow.trans_class,
                        item = firstRow.item,
                        whse = firstRow.whse,
                        shift = firstRow.shift,
                        posted = firstRow.posted,
                        issue_parent = firstRow.issue_parent,

                        job = dto.JobNumber,
                        oper_num = dto.OperationNumber,
                        wc = dto.Wc,
                        SerialNo = dto.SerialNo,

                        next_oper = nextOper,
                        trans_type = lastTransType,

                        trans_num = currentTransNum,
                        qty_complete = 1,
                        qty_scrapped = 0,
                        qty_moved = 1,
                        close_job = closeJobFlag,
                        complete_op = 1,
                        completed_flag = true,
                        pay_rate = "O",
                        job_rate = regRate,
                        a_hrs = totalHours,
                        a_dollar = jobTranRows.Sum(x => x.a_dollar),
                        start_time = firstRow.start_time,
                        end_time = firstRow.start_time.Value.AddHours((double)totalHours), 

                        status = "3",
                        trans_date = DateTime.Now,
                        RecordDate = DateTime.Now,
                        CreateDate = DateTime.Now,
                        CreatedBy = firstRow.UpdatedBy,
                        UpdatedBy = firstRow.UpdatedBy,
                        RowPointer = Guid.NewGuid()
                    });

                     _context.JobTranMst.AddRange(jobTranRows);
                    await _context.SaveChangesAsync();

                    var deletesytelineTrans = await _sytelineService.DeleteFromSyteLineAsync(dto.JobNumber,dto.SerialNo,dto.Wc,dto.OperationNumber);

                        if (deletesytelineTrans)
                        {
                            var status1Rows = await _context.JobTranMst
                            .Where(j =>
                                j.job == dto.JobNumber &&
                                j.SerialNo == dto.SerialNo &&
                                j.wc == dto.Wc &&
                                j.oper_num == dto.OperationNumber &&
                                j.status == "1")
                            .OrderBy(j => j.trans_num)
                            .ToListAsync();

                            int totalRows = status1Rows.Count;

                            for (int i = 0; i < totalRows; i++)
                            {
                                var row = status1Rows[i];

                                if (row.start_time.HasValue)
                                {
                                    row.start_time = DateTime.Today
                                        .AddSeconds(row.start_time.Value.TimeOfDay.TotalSeconds);
                                }

                                if (row.end_time.HasValue)
                                {
                                    row.end_time = DateTime.Today
                                        .AddSeconds(row.end_time.Value.TimeOfDay.TotalSeconds);
                                }

                                row.qty_moved = 0;
                                row.complete_op = 0;
                                row.qty_complete = 0;

                                
                                if (i == totalRows - 1)
                                {
                                    row.qty_moved = 1;
                                    row.complete_op = 1;
                                    row.qty_complete = 1;
                                    if (totalRows > 1)
                                    {
                                        row.shift = "2";
                                    }
                                }
                                
                                
                                int? sytelineTransNum =
                                    await _sytelineService.InsertJobTranAsync(row, (int)row.complete_op);
                            
                                if (sytelineTransNum == null)
                                {
                                    return StatusCode(500,
                                        $"SyteLine insert failed for Job={row.job}, Oper={row.oper_num}");
                                }
                            
                                row.import_doc_id = sytelineTransNum.ToString();
                            
                            }
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

        if (!dto.StartTime.HasValue || !dto.EndTime.HasValue)
            return BadRequest("StartTime and EndTime are required.");

        try
        {
            var oldRows = await _context.JobTranMst
                            .Where(j =>
                                j.job == dto.Job &&
                                j.SerialNo == dto.SerialNumber &&
                                j.oper_num == dto.OperationNumber &&
                                j.wc == dto.WorkCenter)
                            .OrderByDescending(j => j.trans_num)
                            .ToListAsync();

                        if (!oldRows.Any())
                            return NotFound("No matching job transactions found.");


            int? nextOper = oldRows .Where(r => r.next_oper != null) .Select(r => r.next_oper) .FirstOrDefault();
            string? lastqcgroup = oldRows .Where(r => r.qcgroup != null) .Select(r => r.qcgroup) .FirstOrDefault();
            string? lastTransType = oldRows .Where(r => !string.IsNullOrWhiteSpace(r.trans_type)) .Select(r => r.trans_type) .FirstOrDefault();


            _context.JobTranMst.RemoveRange(oldRows);
            await _context.SaveChangesAsync();

            // Calculate total hours
            decimal totalHours =
                (decimal)(dto.EndTime.Value - dto.StartTime.Value).TotalHours;

            totalHours = Math.Round(totalHours, 4);

            // Generate base trans_num
            decimal trans_num =
                (_context.JobTranMst.Max(x => (decimal?)x.trans_num) ?? 0) + 1;

            // Get employee REG / OT rate
            var empRates = await _context.EmployeeMst
                .Where(e => e.emp_num == dto.EmpNum)
                .Select(e => new { RegRate = e.mfg_reg_rate, OtRate = e.mfg_ot_rate })
                .FirstOrDefaultAsync();

            decimal regRate = empRates?.RegRate ?? 0m;

             // Complete job flag if all opr done
                var routeOperations = await _context.JobRouteMst
                .Where(r => r.Job == dto.Job)
                .Select(r => r.OperNum)
                .Distinct()
                .ToListAsync();

                var completedOperations = await _context.JobTranMst
                .Where(t => t.job == dto.Job
                        && t.status == "3")
                .Select(t => t.oper_num)
                .Distinct()
                .ToListAsync();

                bool allOperationsCompleted = routeOperations
                    .All(op => completedOperations.Contains(op));

                byte closeJobFlag = (byte)(allOperationsCompleted ? 1 : 0);

            // ================== ≤ 8 HOURS ==================
            if (totalHours <= 8)
            {
                // -------- STATUS = 1 (OPEN) --------
                var rowStatus1 = new JobTranMst
                {
                    site_ref = "DEFAULT",
                    suffix = 0,
                    trans_num = trans_num,
                    job = dto.Job,
                    SerialNo = dto.SerialNumber,
                    wc = dto.WorkCenter,
                    machine_id = dto.MachineNum,
                    emp_num = dto.EmpNum,
                    oper_num = dto.OperationNumber,
                    next_oper = nextOper,

                    qty_complete = 0,
                    qty_scrapped = 0,
                    qty_moved = 0,

                    close_job = 0,
                    complete_op = 0,
                    completed_flag = false,

                    trans_type = lastTransType,
                    pay_rate = "R",
                    whse = "MAIN",
                    issue_parent = 0,
                    shift = dto.Shift ?? "1",
                    posted = 1,

                    job_rate = regRate,
                    a_hrs = totalHours,
                    a_dollar = totalHours * regRate,

                    start_time = dto.StartTime,
                    end_time = dto.EndTime,
                    status = "1",

                    qcgroup = lastqcgroup,

                    trans_date = DateTime.Now,
                    RecordDate = DateTime.Now,
                    CreateDate = DateTime.Now,
                    CreatedBy = dto.UpdatedBy,
                    UpdatedBy = dto.UpdatedBy,

                    RowPointer = Guid.NewGuid()
                };

                                
                // -------- STATUS = 3 (COMPLETED) --------
                var rowStatus3 = new JobTranMst
                {
                    // same as rowStatus1
                    site_ref = rowStatus1.site_ref,
                    suffix = rowStatus1.suffix,
                    job = rowStatus1.job,
                    SerialNo = rowStatus1.SerialNo,
                    wc = rowStatus1.wc,
                    machine_id = rowStatus1.machine_id,
                    emp_num = rowStatus1.emp_num,
                    oper_num = rowStatus1.oper_num,
                    next_oper = rowStatus1.next_oper,
                    
                    pay_rate = rowStatus1.pay_rate,
                    whse = rowStatus1.whse,
                    issue_parent = rowStatus1.issue_parent,
                    shift = rowStatus1.shift,
                    posted = rowStatus1.posted,

                    job_rate = rowStatus1.job_rate,
                    a_hrs = rowStatus1.a_hrs,
                    a_dollar = rowStatus1.a_dollar,
                    start_time = rowStatus1.start_time,
                    end_time = rowStatus1.end_time,                    

                    trans_date = rowStatus1.trans_date,
                    RecordDate = rowStatus1.RecordDate,
                    CreateDate = rowStatus1.CreateDate,
                    CreatedBy = rowStatus1.CreatedBy,
                    UpdatedBy = rowStatus1.UpdatedBy,

                    trans_type = lastTransType,                  
                    qty_complete = 1,
                    qty_scrapped = 0,
                    qty_moved = 1,
                    close_job = closeJobFlag,
                    complete_op = 1,
                    completed_flag = true,
                    trans_num = rowStatus1.trans_num + 1,
                    status = "3",
                    qcgroup = lastqcgroup,
                    

                    RowPointer = Guid.NewGuid()
                };

                _context.JobTranMst.AddRange(rowStatus1, rowStatus3);
                await _context.SaveChangesAsync();

                var deletesytelineTrans8 = await _sytelineService.DeleteFromSyteLineAsync(dto.Job,dto.SerialNumber,dto.WorkCenter,dto.OperationNumber);

                if (deletesytelineTrans8)
                {
                    var status1Rows = await _context.JobTranMst
                    .Where(j =>
                        j.job == dto.Job &&
                        j.SerialNo == dto.SerialNumber &&
                        j.wc == dto.WorkCenter &&
                        j.oper_num == dto.OperationNumber &&
                        j.status == "1")
                    .OrderBy(j => j.trans_num)
                    .ToListAsync();

                    int totalRows = status1Rows.Count;

                    for (int i = 0; i < totalRows; i++)
                    {
                        var row = status1Rows[i];

                        if (row.start_time.HasValue)
                        {
                            row.start_time = DateTime.Today
                                .AddSeconds(row.start_time.Value.TimeOfDay.TotalSeconds);
                        }

                        if (row.end_time.HasValue)
                        {
                            row.end_time = DateTime.Today
                                .AddSeconds(row.end_time.Value.TimeOfDay.TotalSeconds);
                        }

                        row.qty_moved = 0;
                        row.complete_op = 0;
                        row.qty_complete = 0;

                        
                        if (i == totalRows - 1)
                        {
                            row.qty_moved = 1;
                            row.complete_op = 1;
                            row.qty_complete = 1;
                            if (totalRows > 1)
                                    {
                                        row.shift = "2";
                                    }
                        }                        
                       
                         // 🔹 Insert into SyteLine
                        int? sytelineTransNum =
                            await _sytelineService.InsertJobTranAsync(row, (int)row.complete_op);
                       
                        if (sytelineTransNum == null)
                        {
                            return StatusCode(500,
                                $"SyteLine insert failed for Job={row.job}, Oper={row.oper_num}");
                        }
                       
                        row.import_doc_id = sytelineTransNum.ToString();
                       
                    }
                    await _context.SaveChangesAsync();

                }

                return Ok(new
                {
                    message = "Job log recreated successfully (≤ 8 hours)",
                    hours = totalHours
                });
            }

            // ================== > 8 HOURS ==================
           
            decimal remainingHours = totalHours;
            decimal currentTransNum = trans_num;

            var jobTranRows = new List<JobTranMst>();

            DateTime currentStartTime = dto.StartTime.Value;

            while (remainingHours > 0)
            {
                decimal hrsForThisRow = remainingHours >= 8 ? 8 : remainingHours;
                bool isRegular = currentTransNum == trans_num;

                DateTime currentEndTime = currentStartTime.AddHours((double)hrsForThisRow);

                decimal rate = isRegular
                    ? regRate
                    : empRates?.OtRate ?? regRate;

                jobTranRows.Add(new JobTranMst
                {
                    site_ref = "DEFAULT",
                    suffix = 0,
                    trans_num = currentTransNum,
                    job = dto.Job,
                    SerialNo = dto.SerialNumber,
                    wc = dto.WorkCenter,
                    machine_id = dto.MachineNum,
                    emp_num = dto.EmpNum,
                    oper_num = dto.OperationNumber,
                    next_oper = nextOper,

                    qty_complete = 0,
                    qty_scrapped = 0,
                    qty_moved = 0,

                    close_job = 0,
                    complete_op = 0,
                    completed_flag = false,

                    trans_type = lastTransType,
                    pay_rate = isRegular ? "R" : "O",
                    whse = "MAIN",
                    issue_parent = 0,
                    shift = dto.Shift ?? "1",
                    posted = 1,

                    job_rate = rate,
                    a_hrs = hrsForThisRow,
                    a_dollar = hrsForThisRow * rate,

                    start_time = currentStartTime,
                    end_time = currentEndTime,

                    status = "1",
                    qcgroup = lastqcgroup,

                    trans_date = DateTime.Now,
                    RecordDate = DateTime.Now,
                    CreateDate = DateTime.Now,
                    CreatedBy = dto.UpdatedBy,
                    UpdatedBy = dto.UpdatedBy,

                    RowPointer = Guid.NewGuid()
                });

                remainingHours -= hrsForThisRow;
                currentTransNum++;
                currentStartTime = currentEndTime;   // 🔑 move clock forward
            }


            jobTranRows.Add(new JobTranMst
            {
                site_ref = "DEFAULT",
                suffix = 0,
                trans_num = currentTransNum,
                job = dto.Job,
                SerialNo = dto.SerialNumber,
                wc = dto.WorkCenter,
                machine_id = dto.MachineNum,
                emp_num = dto.EmpNum,
                oper_num = dto.OperationNumber,
                next_oper = nextOper,

                trans_type = lastTransType,
                qty_complete = 1,
                qty_scrapped = 0,
                qty_moved = 1,

                close_job = closeJobFlag,
                complete_op = 1,
                completed_flag = true,

                pay_rate = "O",
                whse = "MAIN",
                issue_parent = 0,
                shift = dto.Shift ?? "1",
                posted = 1,

                job_rate = regRate,
                a_hrs = totalHours,
                a_dollar = jobTranRows.Sum(x => x.a_dollar),

                start_time = dto.StartTime,
                end_time = dto.EndTime,

                status = "3",
                qcgroup = lastqcgroup,
                

                trans_date = DateTime.Now,
                RecordDate = DateTime.Now,
                CreateDate = DateTime.Now,
                CreatedBy = dto.UpdatedBy,
                UpdatedBy = dto.UpdatedBy,

                RowPointer = Guid.NewGuid()
            });


            _context.JobTranMst.AddRange(jobTranRows);
            await _context.SaveChangesAsync();

            var deletesytelineTrans = await _sytelineService.DeleteFromSyteLineAsync(dto.Job,dto.SerialNumber,dto.WorkCenter,dto.OperationNumber);

                if (deletesytelineTrans)
                {
                    var status1Rows = await _context.JobTranMst
                    .Where(j =>
                        j.job == dto.Job &&
                        j.SerialNo == dto.SerialNumber &&
                        j.wc == dto.WorkCenter &&
                        j.oper_num == dto.OperationNumber &&
                        j.status == "1")
                    .OrderBy(j => j.trans_num)
                    .ToListAsync();

                    int totalRows = status1Rows.Count;

                    for (int i = 0; i < totalRows; i++)
                    {
                        var row = status1Rows[i];

                        if (row.start_time.HasValue)
                        {
                            row.start_time = DateTime.Today
                                .AddSeconds(row.start_time.Value.TimeOfDay.TotalSeconds);
                        }

                        if (row.end_time.HasValue)
                        {
                            row.end_time = DateTime.Today
                                .AddSeconds(row.end_time.Value.TimeOfDay.TotalSeconds);
                        }

                        row.qty_moved = 0;
                        row.complete_op = 0;
                        row.qty_complete = 0;

                        
                        if (i == totalRows - 1)
                        {
                            row.qty_moved = 1;
                            row.complete_op = 1;
                            row.qty_complete = 1;
                            if (totalRows > 1)
                                    {
                                        row.shift = "2";
                                    }
                        }
                        
                        
                        int? sytelineTransNum =
                            await _sytelineService.InsertJobTranAsync(row, (int)row.complete_op);
                       
                        if (sytelineTransNum == null)
                        {
                            return StatusCode(500,
                                $"SyteLine insert failed for Job={row.job}, Oper={row.oper_num}");
                        }
                       
                        row.import_doc_id = sytelineTransNum.ToString();
                       
                    }
                    await _context.SaveChangesAsync();

                }

            return Ok(new
            {
                message = "Job log recreated successfully (> 8 hours)",
                totalHours,
                rowsInserted = jobTranRows.Count
            });

        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
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
            // var sytelineService = new SytelineService(_configuration);
            // await sytelineService.InsertEmployeeAsync(employee);

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

    
    
    [HttpPost("startIssueJob")]
    public async Task<IActionResult> StartIssueJob([FromBody] StartJobRequestDto jobDto)
    {
        if (jobDto == null)
            return BadRequest("Invalid request");

        try
        {        

            int? nextOper = await _context.JobRouteMst
                .Where(r => r.Job == jobDto.JobNumber
                        && r.OperNum > jobDto.OperationNumber)
                .OrderBy(r => r.OperNum)
                .Select(r => (int?)r.OperNum)
                .FirstOrDefaultAsync();        

            decimal startTransNum =
                (_context.JobTranMst.Max(j => (decimal?)j.trans_num) ?? 0) + 1;

            decimal completeTransNum = startTransNum + 1;

            // --------------------------------------------------
            // 2️⃣ START ROW (STATUS = 1)
            // --------------------------------------------------
            var startTran = new JobTranMst
            {
                    site_ref = "DEFAULT",
                    trans_num = startTransNum,
                    job = jobDto.JobNumber,
                    SerialNo = jobDto.SerialNo,                
                    wc = jobDto.Wc,
                    emp_num = jobDto.EmpNum,
                    qty_complete = 0,
                    oper_num = jobDto.OperationNumber,
                    next_oper = nextOper,
                    trans_date = DateTime.Now,
                    RecordDate = DateTime.Now,
                    CreateDate = DateTime.Now,
                    CreatedBy = jobDto.loginuser,
                    UpdatedBy = jobDto.loginuser,
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
                    job_rate = 0,
                    Uf_MovedOKToStock = 0,
                    start_time = DateTime.Now,
                    end_time = DateTime.Now,
                    status = "1",
                    qcgroup = "",
                    RowPointer = Guid.NewGuid()
            };

            _context.JobTranMst.Add(startTran);
            await _context.SaveChangesAsync();

            // --------------------------------------------------
            // 3️⃣ COMPLETE ROW (STATUS = 3)
            // --------------------------------------------------
            var completeTran = new JobTranMst
            {
                    site_ref = "DEFAULT",
                    trans_num = completeTransNum,
                    job = jobDto.JobNumber,
                    SerialNo = jobDto.SerialNo,                
                    wc = jobDto.Wc,
                    emp_num = jobDto.EmpNum,
                    qty_complete = 1,
                    oper_num = jobDto.OperationNumber,
                    next_oper = nextOper,
                    trans_date = DateTime.Now,
                    RecordDate = DateTime.Now,
                    CreateDate = DateTime.Now,
                    CreatedBy = jobDto.loginuser,
                    UpdatedBy = jobDto.loginuser,
                    completed_flag = true,
                    suffix = 0,
                    trans_type = "M",
                    qty_scrapped = 0,
                    qty_moved = 1,
                    pay_rate = "R",
                    whse = "MAIN",
                    close_job = 0,
                    issue_parent = 0,
                    complete_op = 1,                
                    shift = "1",
                    posted = 1,
                    job_rate = 0,
                    Uf_MovedOKToStock = 0,
                    start_time = DateTime.Now,
                    end_time = DateTime.Now,
                    status = "3",
                    qcgroup = "",
                    RowPointer = Guid.NewGuid()
            };

            _context.JobTranMst.Add(completeTran);
            await _context.SaveChangesAsync();
           

           var lastTwoRows = await _context.JobTranMst
                .Where(j => j.job == jobDto.JobNumber
                        && j.oper_num == jobDto.OperationNumber
                        && j.wc == jobDto.Wc
                        && j.SerialNo == jobDto.SerialNo)
                .OrderByDescending(j => j.trans_num)
                .Take(2)
                .ToListAsync();

            var latestRow = lastTwoRows.FirstOrDefault();        // status = 3
            var secondLastRow = lastTwoRows.Skip(1).FirstOrDefault(); // status = 1

            // ✅ CORRECT CONDITION
            if (latestRow?.status == "3" && secondLastRow?.status == "1")
            {
                int comjob = 1; // completed

                var sytelineTransNum =
                    await _sytelineService.InsertJobTranAsync(latestRow, comjob);

                if (sytelineTransNum != null)
                {
                    latestRow.import_doc_id = sytelineTransNum.Value.ToString();
                    _context.JobTranMst.Update(latestRow);
                    await _context.SaveChangesAsync();
                }
            }


            return Ok(new
            {
                success = true,
                message = "Issue job processed successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error while issuing job",
                error = ex.Message
            });
        }
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
            // 🔥 Find current operation index in the job route list
            var prevIndex  = jobRoutes.FindIndex(r => r.OperNum == jobDto.OperationNumber);
 
            // 🔥 Determine previous operation dynamically
            var prevOper = prevIndex  > 0 ? jobRoutes[prevIndex  - 1] : null;
 
            if (prevOper != null)
            {
                // Bypass rule: if previous dept == "OPR" and WC contains "issue"
                if (!(prevOper.dept == "OPR" && prevOper.Wc.ToLower().Contains("issue")))
                {
                    // Check if previous operation completed (status = 3)
                    var prevStatus = await _context.JobTranMst
                        .Where(t => t.job == jobDto.JobNumber && t.oper_num == prevOper.OperNum)
                        .OrderByDescending(t => t.trans_date)
                        .Select(t => t.status)
                        .FirstOrDefaultAsync();
 
                    if (prevStatus != "3")
                    {
                        return BadRequest(new
                        {
                            message = $"Previous operation ({prevOper.OperNum}) not completed."
                        });
                    }
                }
            }
            // 🔥 If no previous operation exists, bypass the check (this is valid)
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

            var routeOperations = await _context.JobRouteMst
            .Where(r => r.Job == dto.JobNumber)
            .Select(r => r.OperNum)
            .Distinct()
            .ToListAsync();

            var completedOperations = await _context.JobTranMst
            .Where(t => t.job == dto.JobNumber
                    && t.status == "1")
            .Select(t => t.oper_num)
            .Distinct()
            .ToListAsync();

            bool allOperationsCompleted = routeOperations
                .All(op => completedOperations.Contains(op));

            int closeJobFlag = allOperationsCompleted ? 1 : 0;




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
                qty_moved = 1,
                pay_rate = "D",
                whse = "MAIN",
                close_job = (byte?)closeJobFlag,
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
                _context.Entry(secondLastRow).State = EntityState.Detached;
                    // Explicitly override only required values
                    secondLastRow.qty_complete = 1;
                    secondLastRow.qty_moved = 1;
                    secondLastRow.close_job = (latestRow?.close_job == 1) ? (byte)1 : (byte)0;
                    var sytelineTransNum = await _sytelineService.InsertJobTranAsync(secondLastRow, 1);

                if (sytelineTransNum != null)
                {
                    latestRow.import_doc_id = sytelineTransNum.Value.ToString();
                    _context.JobTranMst.Update(secondLastRow);
                    await _context.SaveChangesAsync();
                }
            }
            

            // CASE 2: latestRow = 3, secondLastRow = 2
           if (latestRow?.status == "3" && secondLastRow?.status == "2")
            {
                var lastImportDocId = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo
                            && !string.IsNullOrEmpty(j.import_doc_id))
                .OrderByDescending(j => j.trans_num)
                .Select(j => j.import_doc_id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(lastImportDocId))
                return BadRequest(new { message = "No Syteline transaction number found to update." });
                
                bool sytelineResult = await _sytelineService.UpdateJobTranCompletionAsync(
                    Convert.ToInt32(lastImportDocId),                // trans_num in Syteline
                    
                    (byte)latestRow.complete_op,                          // byte
                    latestRow.close_job ?? 0,                       // byte
                    latestRow.qty_complete ?? 0,
                    latestRow.qty_moved ?? 0
                );
                
            }



            // OT logic starts
            Console.WriteLine($"Starting OT logic here ..");

              var runningJobs = await _context.JobTranMst
                .Where(j => j.job == dto.JobNumber
                            && j.oper_num == dto.OperationNumber
                            && j.wc == dto.Wc
                            && j.SerialNo == dto.SerialNo
                            && j.status == "1")
                .OrderBy(j => j.trans_date)
                .ToListAsync();

            decimal totalAhrs = runningJobs.Sum(j => j.a_hrs ?? 0m);

            if (totalAhrs > 8m)
            {
                var firstRow = runningJobs.FirstOrDefault();
                if (firstRow != null && firstRow.start_time.HasValue)
                {
                    decimal extraHours = totalAhrs - 8m;

                    // Pull relevant data from first row
                    int? nextOper = firstRow.next_oper;
                    string? lastQcGroup = firstRow.qcgroup;
                    string? lastTransType = firstRow.trans_type;

                    // Remove old running rows
                    _context.JobTranMst.RemoveRange(runningJobs);
                    await _context.SaveChangesAsync();

                    totalHours = totalAhrs;

                    decimal transNum = (_context.JobTranMst.Max(x => (decimal?)x.trans_num) ?? 0) + 1;

                    // Get employee REG / OT rate
                    empRates = await _context.EmployeeMst
                        .Where(e => e.emp_num == dto.EmpNum)
                        .Select(e => new { RegRate = e.mfg_reg_rate, OtRate = e.mfg_ot_rate })
                        .FirstOrDefaultAsync();

                    regRate = empRates?.RegRate ?? 0m;
                    otRate = empRates?.OtRate ?? regRate;

                    // Check if all operations completed
                    var routeOps = await _context.JobRouteMst
                        .Where(r => r.Job == dto.JobNumber)
                        .Select(r => r.OperNum)
                        .Distinct()
                        .ToListAsync();

                    var completedOps = await _context.JobTranMst
                        .Where(t => t.job == dto.JobNumber && t.status == "3")
                        .Select(t => t.oper_num)
                        .Distinct()
                        .ToListAsync();

                    bool allOpsCompleted = routeOps.All(op => completedOps.Contains(op));
                    closeJobFlag = (allOpsCompleted ? 1 : 0);

                    // Build new JobTran rows using firstRow as base
                    var jobTranRows = new List<JobTranMst>();
                    DateTime currentStartTime = firstRow.start_time.Value;
                    decimal remainingHours = totalHours;
                    decimal currentTransNum = transNum;

                    while (remainingHours > 0)
                    {
                        decimal hrsForThisRow = remainingHours >= 8 ? 8 : remainingHours;
                        bool isRegular = currentTransNum == transNum;

                        DateTime currentEndTime = currentStartTime.AddHours((double)hrsForThisRow);
                        decimal rate = isRegular ? regRate : otRate;

                        jobTranRows.Add(new JobTranMst
                        {
                            // Inherit from firstRow
                            site_ref = firstRow.site_ref,
                            suffix = firstRow.suffix,
                            machine_id = firstRow.machine_id,
                            emp_num = firstRow.emp_num,
                            qcgroup = lastQcGroup,
                            trans_class = firstRow.trans_class,
                            item = firstRow.item,
                            whse = firstRow.whse,
                            shift = firstRow.shift,
                            posted = firstRow.posted,
                            issue_parent = firstRow.issue_parent,

                            // Use DTO for the 4 key fields
                            job = dto.JobNumber,
                            oper_num = dto.OperationNumber,
                            wc = dto.Wc,
                            SerialNo = dto.SerialNo,

                            next_oper = nextOper,
                            trans_type = lastTransType,

                            // OT / calculated values
                            trans_num = currentTransNum,
                            qty_complete = 0,
                            qty_scrapped = 0,
                            qty_moved = 0,
                            close_job = 0,
                            complete_op = 0,
                            completed_flag = false,
                            pay_rate = isRegular ? "R" : "O",
                            job_rate = rate,
                            a_hrs = hrsForThisRow,
                            a_dollar = hrsForThisRow * rate,
                            start_time = currentStartTime,
                            end_time = currentEndTime,

                            status = "1",
                            trans_date = DateTime.Now,
                            RecordDate = DateTime.Now,
                            CreateDate = DateTime.Now,
                            CreatedBy = firstRow.CreatedBy,
                            UpdatedBy = firstRow.UpdatedBy,
                            RowPointer = Guid.NewGuid()
                        });

                        remainingHours -= hrsForThisRow;
                        currentTransNum++;
                        currentStartTime = currentEndTime;   // move clock forward
                    }

                    // Final completed row
                    jobTranRows.Add(new JobTranMst
                    {
                        site_ref = firstRow.site_ref,
                        suffix = firstRow.suffix,
                        machine_id = firstRow.machine_id,
                        emp_num = firstRow.emp_num,
                        qcgroup = lastQcGroup,
                        trans_class = firstRow.trans_class,
                        item = firstRow.item,
                        whse = firstRow.whse,
                        shift = firstRow.shift,
                        posted = firstRow.posted,
                        issue_parent = firstRow.issue_parent,

                        job = dto.JobNumber,
                        oper_num = dto.OperationNumber,
                        wc = dto.Wc,
                        SerialNo = dto.SerialNo,

                        next_oper = nextOper,
                        trans_type = lastTransType,

                        trans_num = currentTransNum,
                        qty_complete = 1,
                        qty_scrapped = 0,
                        qty_moved = 1,
                        close_job = (byte?)closeJobFlag,
                        complete_op = 1,
                        completed_flag = true,
                        pay_rate = "O",
                        job_rate = regRate,
                        a_hrs = totalHours,
                        a_dollar = jobTranRows.Sum(x => x.a_dollar),
                        start_time = firstRow.start_time,
                        end_time = firstRow.start_time.Value.AddHours((double)totalHours), 

                        status = "3",
                        trans_date = DateTime.Now,
                        RecordDate = DateTime.Now,
                        CreateDate = DateTime.Now,
                        CreatedBy = firstRow.UpdatedBy,
                        UpdatedBy = firstRow.UpdatedBy,
                        RowPointer = Guid.NewGuid()
                    });

                     _context.JobTranMst.AddRange(jobTranRows);
                    await _context.SaveChangesAsync();

                    var deletesytelineTrans = await _sytelineService.DeleteFromSyteLineAsync(dto.JobNumber,dto.SerialNo,dto.Wc,dto.OperationNumber);

                        if (deletesytelineTrans)
                        {
                            var status1Rows = await _context.JobTranMst
                            .Where(j =>
                                j.job == dto.JobNumber &&
                                j.SerialNo == dto.SerialNo &&
                                j.wc == dto.Wc &&
                                j.oper_num == dto.OperationNumber &&
                                j.status == "1")
                            .OrderBy(j => j.trans_num)
                            .ToListAsync();

                            int totalRows = status1Rows.Count;

                            for (int i = 0; i < totalRows; i++)
                            {
                                var row = status1Rows[i];

                                if (row.start_time.HasValue)
                                {
                                    row.start_time = DateTime.Today
                                        .AddSeconds(row.start_time.Value.TimeOfDay.TotalSeconds);
                                }

                                if (row.end_time.HasValue)
                                {
                                    row.end_time = DateTime.Today
                                        .AddSeconds(row.end_time.Value.TimeOfDay.TotalSeconds);
                                }

                                row.qty_moved = 0;
                                row.complete_op = 0;
                                row.qty_complete = 0;

                                
                                if (i == totalRows - 1)
                                {
                                    row.qty_moved = 1;
                                    row.complete_op = 1;
                                    row.qty_complete = 1;
                                    if (totalRows > 1)
                                    {
                                        row.shift = "2";
                                    }
                                }
                                
                                
                                int? sytelineTransNum =
                                    await _sytelineService.InsertJobTranAsync(row, (int)row.complete_op);
                            
                                if (sytelineTransNum == null)
                                {
                                    return StatusCode(500,
                                        $"SyteLine insert failed for Job={row.job}, Oper={row.oper_num}");
                                }
                            
                                row.import_doc_id = sytelineTransNum.ToString();
                            
                            }
                            await _context.SaveChangesAsync();

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
public async Task<IActionResult> GetTransactionOverview(
    [FromBody] TransactionOverviewRequest request)
{
    try
    {
        var now = DateTime.Now;

        bool todayOnly = request.todayOnly == 1;
        bool includeTransaction = request.includeTransaction == 1;
        bool includeQC = request.includeQC == 1;
        bool includeVerify = request.IncludeVerify == 1;

        var query = _context.JobTranMst.AsQueryable();

        // ================================
        // TODAY FILTER (OPTIONAL)
        // ================================
        if (todayOnly)
        {
            var today = now.Date;
            query = query.Where(j =>
                j.trans_date.HasValue &&
                j.trans_date.Value.Date == today);
        }

        // ================================
        // GET LATEST TRANSACTION PER JOB
        // ================================
        async Task<List<JobTranMst>> GetLatestJobs(IQueryable<JobTranMst> q)
        {
            return await q
                .GroupBy(j => new
                {
                    j.job,
                    j.SerialNo,
                    j.wc,
                    j.oper_num
                })
                .Select(g => g
                    .OrderByDescending(x => x.trans_num)
                    .FirstOrDefault()!)
                .ToListAsync();
        }

        var normalJobs = includeTransaction
            ? await GetLatestJobs(query.Where(j => j.trans_type != "M" && j.wc != "VERIFY"))
            : new List<JobTranMst>();

        var qcJobs = includeQC
            ? await GetLatestJobs(query.Where(j => j.trans_type == "M"))
            : new List<JobTranMst>();

        var verifyJobs = includeVerify
            ? await GetLatestJobs(query.Where(j => j.wc == "VERIFY"))
            : new List<JobTranMst>();

        // ================================
        // COMMON CALCULATIONS
        // ================================
        int CountRunning(List<JobTranMst> list) =>
            list.Count(j => j.status == "1");

        int CountPaused(List<JobTranMst> list) =>
            list.Count(j => j.status == "2");

        int CountExtended(List<JobTranMst> list) =>
            list.Count(j =>
                j.status == "3" &&
                j.a_hrs.HasValue &&
                j.a_hrs.Value > 8);

        int CountOngoingCritical(List<JobTranMst> list) =>
            list.Count(j =>
                (j.status == "1" || j.status == "2") &&  
                j.start_time.HasValue &&
                (now - j.start_time.Value).TotalHours > 8);

        int CountNormalCompleted(List<JobTranMst> list) =>
            list.Count(j =>
                j.status == "3" &&
                (!j.a_hrs.HasValue || j.a_hrs.Value <= 8));


        // ================================
        // BUILD OVERVIEWS
        // ================================
        var transactionOverview = includeTransaction ? new
        {
            RunningJobs = CountRunning(normalJobs),
            PausedJobs = CountPaused(normalJobs),
            ExtendedJobs = CountExtended(normalJobs),
            NormalCompletedJobs = CountNormalCompleted(normalJobs),
            OngoingCriticalJobs = CountOngoingCritical(normalJobs)
        } : null;

        var qcOverview = includeQC ? new
        {
            RunningQCJobs = CountRunning(qcJobs),
            PausedQCJobs = CountPaused(qcJobs),
            ExtendedQCJobs = CountExtended(qcJobs),
            NormalCompletedQCJobs = CountNormalCompleted(qcJobs),
            OngoingCriticalQCJobs = CountOngoingCritical(qcJobs)
        } : null;

        var verifyOverview = includeVerify ? new
        {
            RunningVerifyJobs = CountRunning(verifyJobs),
            PausedVerifyJobs = CountPaused(verifyJobs),
            ExtendedVerifyJobs = CountExtended(verifyJobs),
            NormalCompletedVerifyJobs = CountNormalCompleted(verifyJobs),
            OngoingCriticalVerifyJobs = CountOngoingCritical(verifyJobs)
        } : null;

        // ================================
        // RESPONSE
        // ================================
        return Ok(new
        {
            success = true,
            todayOnly,
            includeTransaction,
            includeQC,
            includeVerify,
            TransactionOverview = transactionOverview,
            QCOverview = qcOverview,
            VerifyOverview = verifyOverview
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            success = false,
            message = "Error while fetching transaction overview",
            error = ex.Message
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
            bool includeVerify = filter.IncludeVerify == 1;

            int pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            int pageSize = filter.PageSize <= 0 ? 50 : filter.PageSize;

            var allTransQuery = _context.JobTranMst.AsQueryable();

            if (todayOnly)
                allTransQuery = allTransQuery.Where(j => j.trans_date.HasValue && j.trans_date.Value.Date == today);

            List<JobTranMst> latestNormal = new();
            List<JobTranMst> latestQC = new();
            List<JobTranMst> latestVerify = new();

            // Fetch latest Transaction Jobs safely (in-memory grouping)
            if (includeTransaction)
            {
                var normalData = await allTransQuery
                    .Where(j => j.trans_type != "M" && j.wc != "VERIFY")
                    .ToListAsync();

                latestNormal = normalData
                    .GroupBy(j => new { j.job, j.SerialNo, j.wc })
                    .Select(g => g.OrderByDescending(x => x.trans_date).First())
                    .ToList();
            }

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

          

            if (includeVerify)
            {
                var verifyQuery = allTransQuery.Where(j => j.wc == "VERIFY");

                if (todayOnly)
                {
                    verifyQuery = verifyQuery.Where(j =>
                        j.trans_date.HasValue &&
                        j.trans_date.Value.Date == today);
                }

                latestVerify = await verifyQuery
                    .GroupBy(j => j.job)
                    .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault()!)
                    .ToListAsync();
            }


            // Merge both and format
           var combined = latestNormal
                .Concat(latestQC)
                .Concat(latestVerify)
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
                    Type = j.wc == "VERIFY"
                        ? "Verify"
                        : j.trans_type == "M"
                            ? "QC"
                            : "Transaction",
                    QcGroup = j.qcgroup
                })
                .OrderByDescending(j => j.Time)
                .ToList();

            var totalRecords = combined.Count;

            // Apply Pagination
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

   [HttpPost("importCalendar")]
    public IActionResult ImportCalendar([FromBody] List<CalendarImportDto> list)
    {
        if (list == null || !list.Any())
            return BadRequest("No data provided");

        foreach (var item in list)
        {
            bool exists = _context.Calendar
                .Any(x => x.date == item.Date);

            if (exists) continue;

            var entity = new Calendar
            {
                date = item.Date,
                flag = item.Flag,
                CalendarDescription = item.CalendarDescription,
                Occasion = item.Occasion
            };

            _context.Calendar.Add(entity);
        }

        _context.SaveChanges();

        return Ok(new { message = "Calendar imported successfully" });
    }


    [HttpPost("notifications")]
    public async Task<IActionResult> RespondToNotification(
        [FromBody] NotificationResponseDto dto)
    {
        var notification = _context.Notification
            .FirstOrDefault(n => n.NotificationID == dto.NotificationID);

        if (notification == null)
            return NotFound("Notification not found");

        // Update notification
        notification.ResponseSubject = dto.ResponseSubject;
        notification.ResponseBody = dto.ResponseBody;
        notification.ResponseStatus = "RESPONDED";
        notification.Status = "RESPONDED";
        notification.UpdatedDate = DateTime.Now;

        _context.SaveChanges();

        // Send email to user
        var emailApiUrl = _configuration["EmailSettings:ApiUrl"];
        using var client = new HttpClient();

        await client.PostAsJsonAsync(emailApiUrl, new
        {
            to = new[] { notification.Email },
            cc = Array.Empty<string>(),
            bcc = Array.Empty<string>(),
            subject = dto.ResponseSubject,
            body = dto.ResponseBody,
            isHtml = true
        });

        return Ok(new { message = "Response sent successfully" });
    }




}

