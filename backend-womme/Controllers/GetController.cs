using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WommeAPI.Data;
using WommeAPI.Models;

namespace WommeAPI.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
[Authorize]
public class GetController : ControllerBase
{
    private readonly AppDbContext _context;

    public GetController(AppDbContext context)
    {
        _context = context;
    }



    [HttpGet]
    public IActionResult GetWorkcenters(int page = 0, int size = 50, string search = "")
    {
        var query =
            from wc in _context.WomWcEmployee
            join emp in _context.EmployeeMst
                on wc.EmpNum equals emp.emp_num
            select new
            {
                wc.Wc,
                wc.EmpNum,
                wc.Description,
                wc.Name,
                emp.womm_id
            };

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(x => (x.Wc ?? "").Contains(search));
        }

        var total = query.Count();

        var data = query
            .OrderByDescending(x => x.Wc)
            .Skip(page * size)
            .Take(size)
            .ToList();

        return Ok(new { data, total });
    }



    [HttpGet]
    public IActionResult GetWorkCenterMaster()
    {
        var data = _context.WcMst
            .Select(wc => new
            {
                Wc = wc.wc,
                Description = wc.description
            })
            .OrderBy(w => w.Wc)
            .ToList();

        return Ok(new { data });
    }





    [HttpGet]
    public async Task<IActionResult> GetPostedTransactions(int page = 0, int size = 50, string search = "")
    {
        // Step 1: Fetch all JobTranMst records
        var jobTrans = await _context.JobTranMst
            .Where(j => j.SerialNo != null)
            .OrderBy(j => j.trans_date)
            .ToListAsync();

        // Step 2: Group by job/operation/serial/wc
        var grouped = jobTrans
            .GroupBy(j => new { j.job, j.oper_num, j.SerialNo, j.wc })
             // .Where(g =>
             // {
             //     // Take latest record in the group
             //     var latest = g.OrderByDescending(x => x.trans_date).FirstOrDefault();
             //     return latest != null && latest.status == "3"; // include group only if latest is 3
             // });
             .Where(g => g.Any(x => x.status == "3" || x.status == "4" || x.status == "5"));

        // Step 3: Flatten group into list of rows
        var result = grouped
            .SelectMany(g =>
                g.Select(jt =>
                {
                    var emp = _context.EmployeeMst.FirstOrDefault(e => e.emp_num == jt.emp_num);
                    var machine = _context.MachineMaster.FirstOrDefault(m => m.MachineNumber == jt.machine_id);

                    return new
                    {
                        trans_num = jt.trans_num,
                        jobNumber = jt.job,
                        operationNumber = jt.oper_num,
                        serialNumber = jt.SerialNo,
                        workCenter = jt.wc,
                        employee_num = jt.emp_num,
                        employee_name = emp != null ? $"{emp.name} ({emp.emp_num})" : jt.emp_num,
                        machine_num = jt.machine_id,
                        machine_name = machine != null ? $"{machine.MachineName} ({machine.MachineNumber})" : jt.machine_id,
                        status = jt.status,
                        start_time = jt.start_time,
                        end_time = jt.end_time,
                        trans_date = jt.trans_date,
                        total_hours = jt.a_hrs ?? 0,
                        total_dollar = jt.a_dollar ?? 0,
                        job_rate = jt.job_rate ?? 0,
                        shift = jt.shift,
                        //    _qtyreleased = jt.qtyreleased


                    };
                })
            )
            .OrderBy(r => r.jobNumber)
            .ThenBy(r => r.operationNumber)
            .ThenBy(r => r.serialNumber)
            .ThenBy(r => r.trans_date)
            .ToList();

        // Step 4: Optional search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            result = result
                .Where(r =>
                    (r.jobNumber ?? "").ToLower().Contains(lowerSearch) ||
                    (r.employee_name ?? "").ToLower().Contains(lowerSearch) ||
                    (r.machine_name ?? "").ToLower().Contains(lowerSearch))
                .ToList();
        }

        // Step 5: Pagination
        var total = result.Count;
        var paged = result
            .Skip(page * size)
            .Take(size)
            .ToList();

        return Ok(new { total, page, size, data = paged });
    }







    [HttpGet]
    public async Task<IActionResult> GetJobPoolAllData(int page = 0, int size = 50, string search = "")
    {
        try
        {
            var query = _context.JobPool.AsQueryable();

            // Optional search (by Job, Item, Employee, or WorkCenter)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(j =>
                    j.Job.Contains(search));
            }

            var totalRecords = await query.CountAsync();

            var result = await query
                .OrderByDescending(j => j.Status_Time)
                .Skip(page * size)
                .Take(size)
                .Select(j => new
                {
                    j.Job,
                    j.TransactionNum,
                    j.Operation,
                    j.WorkCenter,
                    j.Employee,
                    j.Machine,
                    j.Qty,
                    j.Item,
                    j.Status_ID,
                    j.Status_Time,
                    j.JobPoolNumber
                })
                .ToListAsync();

            return Ok(new { total = totalRecords, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }









    [HttpGet]
    public IActionResult GetEmployees(int page = 0, int size = 50, string search = "")
    {
        var query = _context.EmployeeMst.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(e => (e.emp_num ?? "").Contains(search));

        var total = query.Count();

        var data = query
            .OrderByDescending(e => e.emp_num) // order for consistent paging
            .Skip(page * size)
            .Take(size)
            .Select(e => new
            {
                e.emp_num,
                e.name,
                e.dept,
                e.emp_type,
                e.pay_freq,
                e.mfg_dt_rate,
                e.mfg_ot_rate,
                e.mfg_reg_rate,
                e.hire_date,
                e.RoleID,
                e.PasswordHash,
                e.site_ref,
                e.IsActive,
                e.ProfileImage,
                e.womm_id,
                e.email_addr
            })
            .ToList();

        return Ok(new { data, total });
    }






    [HttpGet]
    public async Task<IActionResult> GetRoleMasters()
    {
        var roles = await _context.RoleMaster.ToListAsync();
        Console.WriteLine($"[GetRoleMasters] Total: {roles.Count}");
        return Ok(roles);
    }

    [HttpGet]
    public async Task<IActionResult> GetPageMasters()
    {
        var pages = await _context.PageMaster.ToListAsync();
        Console.WriteLine($"[GetPageMasters] Total: {pages.Count}");
        return Ok(pages);
    }

    [HttpGet]
    public async Task<IActionResult> RolePageMappings([FromQuery] int? roleID)
    {
        try
        {
            var query = _context.RolePageMapping
                .Join(
                    _context.RoleMaster,
                    rpm => rpm.RoleID,
                    rm => rm.RoleID,
                    (rpm, rm) => new { rpm, rm }
                )
                .Join(
                    _context.PageMaster,
                    temp => temp.rpm.PageID,
                    pm => pm.PageID,
                    (temp, pm) => new
                    {
                        EntryNo = temp.rpm.EntryNo,
                        RoleID = temp.rpm.RoleID,
                        RoleName = temp.rm.RoleName,
                        PageID = temp.rpm.PageID,
                        PageName = pm.PageName,
                        PageUrl = pm.PageURL,
                        CreatedAt = temp.rpm.CreatedAt
                    }
                );

            // Apply optional filter
            if (roleID.HasValue)
            {
                query = query.Where(x => x.RoleID == roleID.Value);
            }

            var mappings = await query
            .OrderByDescending(x => x.PageID == 1) // PageID 1 comes first
            .ThenBy(x => x.EntryNo)
            .ToListAsync();

            return Ok(mappings);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetItems(int page = 0, int size = 50, string search = "")
    {
        var query = _context.ItemMst.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(i => (i.item ?? "").Contains(search));

        var total = await query.CountAsync();

        var data = await query
            .OrderBy(i => i.item) // order for consistent paging 
            .Skip(page * size)
            .Take(size)
            .Select(i => new
            {
                i.item,
                description = i.description ?? "",
                Lot_Size = i.Lot_Size ?? 0,
                Abc_Code = i.Abc_Code ?? "",
                Drawing_Nbr = i.Drawing_Nbr ?? "",
                Product_Code = i.Product_Code ?? "",
                P_M_T_Code = i.P_M_T_Code ?? "",
                Weight_Units = i.Weight_Units ?? "",
            })
            .ToListAsync();

        return Ok(new { data, total });
    }


    [HttpGet]
    public async Task<IActionResult> GetMachineMasters()
    {
        var machines = await _context.MachineMaster.ToListAsync();
        Console.WriteLine($"[GetMachineMasters] Total: {machines.Count}");
        return Ok(machines);
    }


    [HttpGet]
    public async Task<IActionResult> usermaster()
    {
        var users = await _context.EmployeeMst.ToListAsync();
        Console.WriteLine($"[GetUserMasters] Total: {users.Count}");
        return Ok(users);
    }


    [HttpGet]
    public async Task<IActionResult> Employeesdetail()
    {
        var usersWithRole = await (
            from user in _context.EmployeeMst
            join role in _context.RoleMaster
                on user.RoleID equals role.RoleID into roleGroup
            from role in roleGroup.DefaultIfEmpty()
            select new
            {
                user.emp_num,
                user.name,
                user.PasswordHash,
                user.RoleID,
                RoleName = role != null ? role.RoleName : "No Role",
                user.IsActive,
                user.ProfileImage
            }
        ).ToListAsync();

        Console.WriteLine($"[GetUserMasters] Total: {usersWithRole.Count}");

        return Ok(usersWithRole);
    }


    [HttpGet]
    public async Task<IActionResult> GetTotalUsers()
    {
        var totalUsers = await _context.EmployeeMst.CountAsync();
        return Ok(new { totalUsers });
    }


    [HttpGet]
    public async Task<IActionResult> getunpostedJobs(string emp_num = "")
    {
        try
        {
            var fromDate = new DateTime(2025, 8, 1);

            // Step 0: Get current employee
            var currentEmp = await _context.EmployeeMst
                .FirstOrDefaultAsync(e => e.emp_num == emp_num);

            bool isOprLevel4 = currentEmp != null && currentEmp.RoleID == 4;

            // Step 1: Get base data
            var baseData = await (
                from jr in _context.JobRouteMst
                join jm in _context.JobMst on jr.Job equals jm.job
                join wm in _context.WcMst on jr.Wc equals wm.wc
                where wm.dept == "OPR" && jm.CreateDate >= fromDate
                select new { jr, jm, wm }
            ).ToListAsync();

            // Step 2: Get WC-Employee mapping
            var wcList = baseData.Select(x => x.jr.Wc).Distinct().ToList();
            var employees = await _context.WomWcEmployee
                .Where(e => wcList.Contains(e.Wc))
                .ToListAsync();

            // Step 3: Restrict for OPR RoleID 4
            if (isOprLevel4)
            {
                var allowedWcForCurrentEmp = employees
                    .Where(e => e.EmpNum == emp_num && e.Wc != null) // ensure Wc is not null
                    .Select(e => e.Wc!)
                    .Distinct()
                    .ToList();

                baseData = baseData
                    .Where(x => x.jr.Wc != null && allowedWcForCurrentEmp.Contains(x.jr.Wc))
                    .ToList();
            }

            // Step 4: Expand qty_released into serial numbers
            var groupedData = baseData
                .GroupBy(x => new { x.jr.Job, x.jr.OperNum, x.jr.Wc, x.jm.qty_released })
                .SelectMany(g =>
                    Enumerable.Range(1, (int)g.Key.qty_released)
                        .Select(i => new { SerialNo = $"{g.Key.Job?.Trim()}-{i}" })
                )
                .ToList();

            var totalUnpostedJobs = groupedData.Count;

            return Ok(new { totalUnpostedJobs });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }



    [HttpGet]
    public async Task<IActionResult> PostedJobsCount()
    {
        try
        {
            // Count jobs in JobTranMst where CompletedFlag == 1
            var totalPostedJobs = await _context.JobTranMst
                .Where(jt => jt.completed_flag == true)
                .CountAsync();

            return Ok(new { totalPostedJobs });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ActiveJobsCount()
    {
        try
        {
            var allJobs = await _context.JobTranMst
                .Where(jt => jt.SerialNo != null)
                .Select(jt => new
                {
                    jt.job,
                    jt.SerialNo,
                    jt.oper_num,
                    jt.wc,
                    jt.status,
                    jt.trans_date
                })
                .ToListAsync();

            var count = allJobs
                .GroupBy(x => new { x.job, x.SerialNo, x.oper_num, x.wc })
                .Select(g => g.OrderByDescending(x => x.trans_date).First())
                .Count(x => x.status == "1");

            return Ok(count);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> PausedJobsCount()
    {
        try
        {
            var allJobs = await _context.JobTranMst
                .Where(jt => jt.SerialNo != null)
                .Select(jt => new
                {
                    jt.job,
                    jt.SerialNo,
                    jt.oper_num,
                    jt.wc,
                    jt.status,
                    jt.trans_date
                })
                .ToListAsync();

            var count = allJobs
                .GroupBy(x => new { x.job, x.SerialNo, x.oper_num, x.wc })
                .Select(g => g.OrderByDescending(x => x.trans_date).First())
                .Count(x => x.status == "2");

            return Ok(count);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExcdJobsCount()
    {
        try
        {
            var now = DateTime.UtcNow;

            var allJobs = await _context.JobTranMst
                .Where(jt => jt.SerialNo != null)
                .Select(jt => new
                {
                    jt.job,
                    jt.SerialNo,
                    jt.oper_num,
                    jt.wc,
                    jt.status,
                    jt.trans_date,
                    jt.a_hrs,
                    jt.start_time,
                    jt.end_time
                })
                .ToListAsync();

            var exceededCount = allJobs
                .GroupBy(x => new { x.job, x.SerialNo, x.oper_num, x.wc })
                .Count(g =>
                {
                    var latest = g.OrderByDescending(x => x.trans_date).First();

                    if (latest.status == "3")
                    {
                        // Completed jobs: only use status=1 rows' a_hrs
                        var totalStatus1 = g
                            .Where(x => x.status == "1")
                            .Sum(x => x.a_hrs ?? 0);

                        return totalStatus1 > 8;
                    }
                    else if (latest.status == "1")
                    {
                        // Active jobs: total a_hrs + running time from start_time
                        double logged = (double)g.Sum(x => x.a_hrs ?? 0);

                        var activeRow = g
                            .Where(x => x.status == "1" && x.end_time == null)
                            .OrderByDescending(x => x.start_time)
                            .FirstOrDefault();

                        if (activeRow?.start_time != null)
                        {
                            logged += (now - activeRow.start_time.Value).TotalHours;
                        }

                        return logged > 8;
                    }

                    return false;
                });

            return Ok(exceededCount);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }











    [HttpGet]
    public async Task<IActionResult> GetActiveOvertimeJobCount()
    {
        var allLogs = await _context.EmployeeLog
            .Where(e => e.StatusID != 4) // Exclude completed jobs early
            .ToListAsync();

        var latestLogs = allLogs
            .GroupBy(e => e.JobNumber)
            .Select(g => g.OrderByDescending(e => e.StatusTime).FirstOrDefault())
            .Where(log => log != null)
            .ToList();

        var jobNumbers = latestLogs.Select(log => log!.JobNumber).ToList();

        var jobs = await _context.Job
            .Where(j => jobNumbers.Contains(j.JobNumber))
            .ToListAsync();

        int overtimeCount = 0;

        foreach (var log in latestLogs)
        {
            var lastActive = allLogs
                .Where(e => e.JobNumber == log!.JobNumber && e.StatusID == 2)
                .OrderByDescending(e => e.StatusTime)
                .FirstOrDefault();

            var job = jobs.FirstOrDefault(j => j.JobNumber == log!.JobNumber);

            if (lastActive != null && job != null && job.EstimatedHours.HasValue)
            {
                var elapsedHours = (DateTime.UtcNow - lastActive.StatusTime).TotalHours;
                if (elapsedHours > (double)job.EstimatedHours.Value)
                {
                    overtimeCount++;
                }
            }
        }

        return Ok(new { count = overtimeCount });
    }

    // report api 



    [HttpGet("{jobId}")]
    public async Task<IActionResult> GetJobReport(string jobId)
    {
        // Step 1: Job header info
        var jobInfo = await (from j in _context.JobMst
                            join i in _context.ItemMst
                                on j.item equals i.item into itemJoin
                            from i in itemJoin.DefaultIfEmpty()
                            where j.job == jobId
                            select new JobReportDto
                            {
                                Job              = j.job,
                                JobDate          = j.job_date,
                                PreparedBy       = j.Uf_JobPreparedBy,
                                MaterialClass    = j.Uf_JobMatlClass,
                                DrawingNo        = j.Uf_DrawingNo,
                                RevisionNo       = j.revision,
                                TempClass        = j.Uf_JobTempClass,
                                DrawingRev       = j.Uf_RouterRevNo,
                                SoNo             = j.ord_num,
                                ReleasedQty      = j.qty_released,
                                CreatedDate      = j.CreateDate,
                                Status           = j.stat,
                                MatlDesc         = j.Uf_MatlDescJob,
                                PSL              = j.Uf_JobPsl,
                                Item             = j.item,
                                ItemDescription  = j.description,
                                JobDueDate       = i != null ? i.CreateDate : (DateTime?)null,
                                Suffix           = j.suffix,
                                UfItemDescription2 = i != null ? i.Uf_ItemDescription2 : null
                            }).FirstOrDefaultAsync();

        if (jobInfo == null)
            return NotFound(new { Message = "Job not found" });

        // Step 2: Fetch operations
        var operations = await (
            from r in _context.JobRouteMst

            join wc in _context.WcMst
                on r.Wc equals wc.wc into wcJoin
            from wc in wcJoin.DefaultIfEmpty()

            join om in _context.OperationMaster
                on r.OperNum equals om.OperationNumber into opJoin
            from om in opJoin.DefaultIfEmpty()

            where r.Job == jobId
            orderby r.OperNum

            select new JobOperationDto
            {
                OperNum  = r.OperNum,
                WC       = r.Wc,
                OperationDescription = om != null
                    ? om.OperationDescription
                    : (wc != null ? wc.description : r.UfTaskDescription),

                Items = _context.JobMatlMst
                    .Where(m => m.Job == jobId && m.OperNum == r.OperNum)
                    .Select(m => new JobOperationItemDto
                    {
                        Item               = m.Item,
                        ItemDescription    = "",
                        RequiredQty        = m.QtyIssued,
                        Sequence           = m.Sequence,
                        UfLastVendName     = m.UfLastVendName ?? "",
                        UfItemDescription2 = ""
                    }).ToList()
            }
        ).ToListAsync();

        // Step 3: Load all transactions separately
        var allTransactions = await _context.JobTranMst
            .Where(t => t.job == jobId)
            .ToListAsync();

        var employees = await _context.EmployeeMst
            .Select(e => new { e.emp_num, e.name })
            .ToListAsync();

        // Step 4: Group and filter in memory
        foreach (var op in operations)
        {
            op.Transactions = allTransactions
                .Where(t => t.oper_num == op.OperNum)
                .OrderBy(t => t.trans_date)
                .Select(t => new JobTransactionDto
                {
                    SerialNo   = t.SerialNo,
                    CreateDate = t.CreateDate,
                    TransDate  = t.trans_date,
                    TransType  = t.trans_type,
                    Status     = t.status,
                    Remark     = t.Remark,
                    MachineId  = t.machine_id,
                    EmployeeId = t.CreatedBy,
                    EmployeeName = employees
                        .FirstOrDefault(e => e.emp_num == t.CreatedBy)?.name
                })
                .ToList();
        }

        // Step 5: Apply header audit overrides
        var auditValues = await _context.JobReportAudit
            .Where(x => x.Job == jobId)
            .ToListAsync();

        foreach (var audit in auditValues)
        {
            var col = audit.ColumnName?.ToLower();

            if (col == "jobdate")      jobInfo.JobDate      = DateTime.Parse(audit.ColumnValue);
            if (col == "jobduedate")   jobInfo.JobDueDate   = DateTime.Parse(audit.ColumnValue);
            if (col == "revisiondate") jobInfo.RevisionDate = DateTime.Parse(audit.ColumnValue);
            if (col == "date")         jobInfo.Date         = DateTime.Parse(audit.ColumnValue);
        }

        // Step 6: Apply transaction audit overrides
        foreach (var op in operations)
        {
            foreach (var tran in op.Transactions)
            {
                var auditDate = auditValues.FirstOrDefault(a =>
                    a.OperationNo == op.OperNum.ToString() &&
                    a.SerialNo    == tran.SerialNo &&
                    a.ColumnName  == "date");

                if (auditDate != null && DateTime.TryParse(auditDate.ColumnValue, out var newDate))
                    tran.TransDate = newDate;

                var auditQty = auditValues.FirstOrDefault(a =>
                    a.OperationNo == op.OperNum.ToString() &&
                    a.SerialNo    == tran.SerialNo &&
                    a.ColumnName  == "qty");

                if (auditQty != null && decimal.TryParse(auditQty.ColumnValue, out var newQty))
                    tran.QtyComplete = newQty;
            }
        }

        
        

        jobInfo.Operations = operations;

        return Ok(jobInfo);
    }




    [HttpGet]
    public async Task<IActionResult> GetQaQcEmployees()
    {
        var employees = await _context.EmployeeMst
            .Where(e => e.dept == "QA/ QC")
            .Select(e => new EmployeeDto
            {
                EmpNum      = e.emp_num,
                Name        = e.name,
                Email       = e.email_addr,
                RoleID      = e.RoleID,
                IsActive    = e.IsActive,
                depart      = e.dept,
                emp_type    = e.emp_type,
                pay_freq    = e.pay_freq
            })
            .ToListAsync();

        if (!employees.Any())
            return NotFound(new { Message = "No QA/QC employees found" });

        return Ok(employees);
    }


    [HttpGet]
    public IActionResult GetDistinctOperations(int page = 0, int size = 50, int? search = null)
    {
        // Step 1: Group by operation number to ensure true distinctness
        var query = _context.JobTranMst
            .GroupBy(j => j.oper_num)
            .Select(g => g.Key);

        // Step 2: Apply search filter (if provided)
        if (search.HasValue)
            query = query.Where(o => o == search.Value);

        // Step 3: Total count
        var total = query.Count();

        // Step 4: Apply pagination
        var data = query
            .OrderByDescending(o => o) // stable paging
            .Skip(page * size)
            .Take(size)
            .ToList();

        // Step 5: Return
        return Ok(new { data, total });
    }





    [HttpGet]
    public IActionResult unpostedCheckJobStatus()
    {
        try
        {
            var mappings = _context.EmployeeLog
                .Select(el => new
                {
                    el.JobNumber,
                    el.EmployeeCode,
                    el.MachineNumber,
                    el.OperationNumber,
                    el.TransNumber,
                    el.StatusID,
                    el.StatusTime,
                    el.CreatedAt,
                    el.UpdatedAt
                })
                .ToList();

            if (mappings == null || !mappings.Any())
                return NotFound(new { message = "No Machine-Employee mappings found." });

            return Ok(mappings);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
        }
    }





    [HttpGet]
    public async Task<IActionResult> GetJobUnpostedTransFullDetails(
        [FromQuery] string job,
        [FromQuery] int operNum,
        [FromQuery] int trans_num)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(job) || operNum <= 0 || trans_num <= 0)
                return BadRequest(new { message = "Invalid job, operation number, or transaction number." });

            // Step 1: Released qty from job_mst
            var jobData = await (from j in _context.JobMst
                                 join i in _context.ItemMst
                                     on j.item equals i.item into itemJoin
                                 from i in itemJoin.DefaultIfEmpty()
                                 where j.job == job
                                 select new
                                 {
                                     j.job,
                                     j.qty_released,
                                     j.item,
                                     ItemDescription = i != null ? i.description : null
                                 }).FirstOrDefaultAsync();

            if (jobData == null)
                return NotFound(new { message = $"Job '{job}' not found." });

            // Step 2: Fetch only that transaction (job + opernum + transnum)
            var jobTran = await _context.JobTranMst
                .Where(t => t.job == job && t.oper_num == operNum && t.trans_num == trans_num)
                .Select(t => new
                {
                    t.trans_num,
                    t.oper_num,
                    t.next_oper,
                    t.wc,
                    t.trans_type
                })
                .FirstOrDefaultAsync();

            if (jobTran == null)
                return NotFound(new { message = $"Transaction {trans_num} not found for Job '{job}' at Operation {operNum}." });

            // Step 3: Employees from WC
            var employees = await (from wcEmp in _context.WomWcEmployee
                                   join em in _context.EmployeeMst
                                       on wcEmp.EmpNum equals em.emp_num
                                   where wcEmp.Wc == jobTran.wc
                                   select new
                                   {
                                       EmpNum = em.emp_num,
                                       Name = em.name,
                                       wc = wcEmp.Wc
                                   }).ToListAsync();

            // Step 4: Machines for these employees
            var employeeIds = employees.Select(e => e.EmpNum).ToList();
            var machines = await (from me in _context.WomMachineEmployee
                                  where employeeIds.Contains(me.Emp_Num)
                                  select new
                                  {
                                      me.Emp_Num,
                                      me.Machine_Num,
                                      me.Machine_Description
                                  }).ToListAsync();

            var employeeWithMachines = employees
                .Select(e => new
                {
                    e.EmpNum,
                    e.Name,
                    e.wc,
                    Machines = machines
                        .Where(m => m.Emp_Num == e.EmpNum)
                        .Select(m => new
                        {
                            m.Machine_Num,
                            m.Machine_Description
                        })
                        .ToList()
                })
                .ToList();

            // Step 5: Response for just this transaction
            var result = new
            {
                Job = jobData.job,
                ReleasedQty = jobData.qty_released,
                OperNum = jobTran.oper_num,
                NextOper = jobTran.next_oper,
                TransNum = jobTran.trans_num,
                WorkCenter = jobTran.wc,
                TransType = jobTran.trans_type,
                Item = jobData.item,
                ItemDescription = jobData.ItemDescription,
                Employees = employeeWithMachines
            };

            return Ok(new[] { result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
        }
    }



    [HttpGet]
    public async Task<IActionResult> GetJobPostedTransFullDetails(
        [FromQuery] string job,
        [FromQuery] string operNum,
        [FromQuery] string trans_num
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(job))
                return BadRequest(new { message = "Invalid input parameters." });

            // Fetch Employee Logs with Employee Name and Machine Description
            var logs = await (from log in _context.EmployeeLog
                              join emp in _context.EmployeeMst
                              on log.EmployeeCode equals emp.emp_num into empJoin
                              from emp in empJoin.DefaultIfEmpty()
                              join mc in _context.MachineMaster
                              on log.MachineNumber equals mc.MachineNumber into mcJoin
                              from mc in mcJoin.DefaultIfEmpty()
                              where log.JobNumber == job
                                      && log.OperationNumber == operNum
                                      && log.TransNumber == trans_num
                              orderby log.serialNo, log.EntryNo
                              select new
                              {
                                  log.EntryNo,
                                  log.serialNo,
                                  log.StatusID,
                                  log.StatusTime,
                                  log.EmployeeCode,
                                  EmployeeName = emp != null ? emp.name : null,
                                  log.MachineNumber,
                                  MachineDescription = mc != null ? mc.MachineDescription : null,
                                  log.JobNumber,
                                  log.OperationNumber,
                                  log.TransNumber,
                                  log.WorkCenter
                              }).ToListAsync();

            if (!logs.Any())
                return NotFound(new { message = "No workflow logs found for the specified job/operation/transaction/workcenter." });

            // Group logs by SerialNo
            var groupedLogs = logs
                .GroupBy(l => l.serialNo)
                .Select(g => new
                {
                    SerialNo = g.Key,
                    LogEntries = g.Select(l => new
                    {
                        l.EntryNo,
                        l.StatusID,
                        l.StatusTime,
                        l.EmployeeCode,
                        l.EmployeeName,
                        l.MachineNumber,
                        l.MachineDescription
                    }).ToList()
                })
                .ToList();

            // Take job info from the first log entry
            var firstLog = logs.First();

            var result = new
            {
                Job = firstLog.JobNumber,
                OperationNumber = firstLog.OperationNumber,
                TransactionNumber = firstLog.TransNumber,
                WorkCenter = firstLog.WorkCenter,
                EmployeeLogs = groupedLogs
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
        }
    }




    [HttpGet]
    public IActionResult GetAllWcMachines()
    {
        try
        {
            // For keyless entities, always use AsNoTracking()
            var machines = _context.WomWcMachines
                .AsNoTracking()
                .OrderByDescending(m => m.RecordDate)
                .Select(m => new
                {
                    Wc = m.Wc ?? string.Empty,
                    MachineId = m.MachineId ?? string.Empty,
                    MachineDescription = m.MachineDescription ?? string.Empty,
                    WcName = m.WcName ?? string.Empty,

                })
                .ToList();

            if (!machines.Any())
                return NotFound(new { message = "No WC Machines found." });

            return Ok(machines);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
        }
    }



    // GET: api/employee/names
    [HttpGet]
    public IActionResult getAllEmployees()
    {
        try
        {
            var employees = _context.EmployeeMst
                .Select(e => new EmployeeNameDto
                {
                    EmpName = e.name,
                    EmpNum = e.emp_num
                })
                .ToList();

            return Ok(employees);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
        }
    }

    // GET: api/machine/names
    [HttpGet]
    public IActionResult getAllMachines()
    {
        try
        {
            var machines = _context.MachineMaster
                .Select(m => new MachineNameDto
                {
                    MachineName = m.MachineName,
                    MachineNumber = m.MachineNumber
                })
                .ToList();

            return Ok(machines);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
        }
    }




    [HttpGet]
    public async Task<IActionResult> GetJobs(int page = 0, int size = 50, string search = "")
    {
        try
        {
            var fromDate = new DateTime(2025, 11, 1);

            // Step 1: Fetch only necessary fields
            var query = from jr in _context.JobRouteMst
                        join jm in _context.JobMst on jr.Job equals jm.job
                        join wm in _context.WcMst on jr.Wc equals wm.wc
                        where jm.CreateDate >= fromDate
                        select new
                        {
                            Job = jr.Job,
                            Quantity = jm.qty_released,
                            Item = jm.item,
                            OperNo = jr.OperNum,
                            WCCode = jr.Wc,
                            Job_date=jm.job_date
                            
                            
                        };

            // Step 2: Apply optional search before materializing
            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(x =>
                    x.Job.ToLower().Contains(lowerSearch) ||
                    x.Item.ToLower().Contains(lowerSearch) ||
                    x.WCCode.ToLower().Contains(lowerSearch));
            }

            // Step 3: Pagination
            var totalRecords = await query.CountAsync();
            var data = await query
                .OrderBy(x => x.Job)
                .ThenBy(x => x.OperNo)
                .Skip(page * size)
                .Take(size)
                .ToListAsync();

            // Step 4: Return result
            return Ok(new
            {
                totalRecords,
                page,
                size,
                data
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }





   [HttpGet]
    public async Task<IActionResult> GetUnpostedTransactions(
        int page = 0,
        int size = 50,
        string search = "",
        string emp_num = "")
    {
        try
        {
            var fromDate = new DateTime(2025, 11, 1);

            // Step 0: Check logged-in employee info
            var currentEmp = await _context.EmployeeMst
                .FirstOrDefaultAsync(e => e.emp_num == emp_num);

            bool isOprLevel4 = currentEmp != null && currentEmp.RoleID == 4;

            // Step 1: Fetch the base data from DB asynchronously
            var baseData = await (
                from jr in _context.JobRouteMst
                join jm in _context.JobMst on jr.Job equals jm.job
                join wm in _context.WcMst on jr.Wc equals wm.wc
                where wm.dept == "OPR" && jm.CreateDate >= fromDate
                && !jr.Wc.ToLower().Contains("issue")
                select new
                {
                    jr,
                    jm,
                    wm
                })
                .ToListAsync();

            // Step 2: Fetch related employees for the WCs
            var wcList = baseData.Select(x => x.jr.Wc).Distinct().ToList();
            var employees = await _context.WomWcEmployee
                .Where(e => wcList.Contains(e.Wc))
                .ToListAsync();

            // Step 3: If this user is OPR+roleID4, only allow jobs of this emp_num
            List<string> allowedWcForCurrentEmp = new();
            if (isOprLevel4)
            {
                allowedWcForCurrentEmp = employees
                    .Where(e => e.EmpNum == emp_num && e.Wc != null)
                    .Select(e => e.Wc!)
                    .Distinct()
                    .ToList();

                baseData = baseData
                    .Where(x => x.jr.Wc != null && allowedWcForCurrentEmp.Contains(x.jr.Wc))
                    .ToList();
            }

            // Step 4: Group by job/oper/wc and expand serial numbers
            var groupedData = baseData
                .GroupBy(x => new { x.jr.Job, x.jr.OperNum, x.jr.Wc, x.jm.qty_released, x.jm.item, x.jm.CreateDate, x.wm.description })
                .Distinct()
                .SelectMany(g =>
                {
                    var empNums  = string.Join(", ", employees.Where(e => e.Wc == g.Key.Wc).Select(e => e.EmpNum));
                    var empNames = string.Join(", ", employees.Where(e => e.Wc == g.Key.Wc).Select(e => e.Name));

                    return Enumerable.Range(1, (int)g.Key.qty_released).Select(i => new UnpostedTransactionDto
                    {
                        SerialNo      = $"{(g.Key.Job ?? "").Trim()}-{i}",
                        Job           = (g.Key.Job ?? "").Trim(),
                        QtyReleased   = 1,
                        Item          = g.Key.item ?? "",
                        JobYear       = g.Key.CreateDate.Year,
                        OperNum       = g.Key.OperNum.ToString(),
                        WcCode        = g.Key.Wc ?? "",
                        WcDescription = g.Key.description ?? "",
                        EmpNum        = empNums,
                        EmpName       = empNames
                    });
                })
                .OrderBy(x => x.Job)
                .ThenBy(x => x.OperNum)
                .ThenBy(x => x.SerialNo)
                .ToList();

            // Step 5: Optional search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                groupedData = groupedData
                    .Where(x =>
                        x.Job.ToLower().Contains(lowerSearch) ||
                        x.Item.ToLower().Contains(lowerSearch) ||
                        (x.EmpName ?? "").ToLower().Contains(lowerSearch) ||
                        (x.MachineDescription ?? "").ToLower().Contains(lowerSearch))
                    .ToList();
            }

            // ── Completed jobs filter ─────────────────────────────────────────
            var completedJobs = await _context.JobTranMst
                .Where(jt => jt.SerialNo != null && jt.status == "3")
                .Select(jt => new { jt.SerialNo, jt.oper_num, jt.wc })
                .ToListAsync();

            var reopenedRows = await _context.JobTranMst
                .Where(jt => jt.SerialNo != null && jt.status == "2" && jt.reopen_flag == true)
                .Select(jt => new { jt.SerialNo, jt.oper_num, jt.wc })
                .ToListAsync();

            var reopenedSet = new HashSet<string>(
                reopenedRows.Select(x => $"{x.SerialNo}-{x.oper_num}-{x.wc}")
            );

            var completedJobSet = new HashSet<string>(
                completedJobs
                    .Select(x => $"{x.SerialNo}-{x.oper_num}-{x.wc}")
                    .Where(key => !reopenedSet.Contains(key))
            );

            groupedData = groupedData
                .Where(g => !completedJobSet.Contains($"{g.SerialNo}-{g.OperNum}-{g.WcCode}"))
                .ToList();

            // ── Step 5.5: Mark active jobs ────────────────────────────────────
            var allSerialNos = groupedData
                .Select(x => x.SerialNo)
                .Where(x => x != null)
                .ToList();

            var latestStatuses = await _context.JobTranMst
                .Where(jt =>
                    jt.SerialNo != null &&
                    allSerialNos.Contains(jt.SerialNo) &&
                    (jt.trans_type == "D" || (jt.trans_type == "M" && jt.reopen_flag != true))
                )
                .GroupBy(jt => new { jt.job, jt.oper_num, jt.SerialNo, jt.wc })
                .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault())
                .ToListAsync();

            var latestStatusDict = latestStatuses
                .Where(x => x != null && x!.SerialNo != null && x.oper_num != null)
                .ToDictionary(
                    x => $"{x!.SerialNo}-{x!.oper_num}",
                    x => x!.status
                );

            foreach (var g in groupedData)
            {
                var key = $"{g.SerialNo}-{g.OperNum}";
                g.IsActive = latestStatusDict.TryGetValue(key, out var status) && status != "3";
            }

            // ── Step 5.6: Fetch audit info per unique Job ─────────────────────
            var uniqueJobs = groupedData
                .Select(x => x.Job)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();

            // Get latest audit record per job in ONE query
            var auditRecords = await _context.JobReportAudit
                .Where(a => a.Job != null && uniqueJobs.Contains(a.Job))
                .GroupBy(a => a.Job)
                .Select(g => g.OrderByDescending(a => a.UpdatedDate).First())
                .ToListAsync();

            // Get all employee names for updatedBy in ONE query
            var updatedByList = auditRecords
                .Select(a => a.UpdatedBy)
                .Where(x => x != null)
                .Distinct()
                .ToList();

            var auditEmployees = await _context.EmployeeMst
                .Where(e => updatedByList.Contains(e.emp_num))
                .Select(e => new { e.emp_num, e.name })
                .ToListAsync();

            var empNameMap = auditEmployees
                .ToDictionary(e => e.emp_num ?? "", e => e.name ?? "");

            // Build audit lookup dictionary: job → { date, name }
            var auditMap = auditRecords.ToDictionary(
                a => a.Job ?? "",
                a => new
                {
                    LastModifiedDate   = a.UpdatedDate,
                    LastModifiedBy     = a.UpdatedBy,
                    LastModifiedByName = empNameMap.TryGetValue(a.UpdatedBy ?? "", out var name)
                                            ? name
                                            : a.UpdatedBy
                }
            );

            // Sort: active jobs first
            groupedData = groupedData
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Job)
                .ThenBy(x => x.OperNum)
                .ThenBy(x => x.SerialNo)
                .ToList();

            // Step 6: Pagination
            var totalRecords = groupedData.Count;
            var pagedData    = groupedData
                .Skip(page * size)
                .Take(size)
                .ToList();

            // Step 7: Attach audit info to each row before returning
            var result = pagedData.Select(g => new
            {
                g.SerialNo,
                g.Job,
                g.QtyReleased,
                g.Item,
                g.JobYear,
                g.OperNum,
                g.WcCode,
                g.WcDescription,
                g.EmpNum,
                g.EmpName,
                g.IsActive,
                g.IsNextJobActive,
                g.Status,
                g.Remark,
                g.MachineId,
                g.MachineDescription,
                g.TransType,
                g.QcGroup,
                g.trans_number,

                // ← NEW audit fields
                LastModifiedDate   = auditMap.TryGetValue(g.Job ?? "", out var audit)
                                        ? audit.LastModifiedDate
                                        : (DateTime?)null,
                LastModifiedBy     = auditMap.TryGetValue(g.Job ?? "", out var audit2)
                                        ? audit2.LastModifiedBy
                                        : null,
                LastModifiedByName = auditMap.TryGetValue(g.Job ?? "", out var audit3)
                                        ? audit3.LastModifiedByName
                                        : null
            }).ToList();

            return Ok(new
            {
                totalRecords,
                page,
                size,
                data = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }



    [HttpGet]
    public async Task<IActionResult> GetIssuedTransactions(
        int page = 0,
        int size = 50,
        string search = "",
        string emp_num = "")
    {
        try
        {
            var fromDate = new DateTime(2025, 11, 1);

            // 🔹 Employee role check
            var currentEmp = await _context.EmployeeMst
                .FirstOrDefaultAsync(e => e.emp_num == emp_num);
            bool isOprLevel4 = currentEmp != null && currentEmp.RoleID == 4;

            // 🔹 STEP 1 — Fetch unique issued job routes
            var baseData = await (
                from jr in _context.JobRouteMst
                join jm in _context.JobMst on jr.Job equals jm.job
                join wm in _context.WcMst on jr.Wc equals wm.wc
                where wm.dept == "OPR"
                && jm.CreateDate >= fromDate
                && jr.Wc.ToLower().Contains("issue")
                select new
                {
                    Job = jr.Job,
                    OperNum = jr.OperNum,
                    Wc = jr.Wc,
                    QtyReleased = jm.qty_released,
                    Item = jm.item,
                    CreateDate = jm.CreateDate,
                    WcDescription = wm.description
                })
                .ToListAsync();

            // ✔ Now remove duplicates purely by Job + Oper + Wc (safe)
            baseData = baseData
                .GroupBy(x => new { x.Job, x.OperNum, x.Wc })
                .Select(g => g.First())
                .ToList();


            // 🔹 STEP 2 — Employee mapping for WCs
            var wcList = baseData.Select(x => x.Wc).Distinct().ToList();
            var employees = await _context.WomWcEmployee
                .Where(e => wcList.Contains(e.Wc))
                .ToListAsync();

            // 🔹 STEP 3 — Role filter (OPR + Level 4 user)
            if (isOprLevel4)
            {
                var allowedWc = employees
                    .Where(e => e.EmpNum == emp_num && e.Wc != null)
                    .Select(e => e.Wc!)
                    .Distinct()
                    .ToList();

                baseData = baseData
                    .Where(x => allowedWc.Contains(x.Wc))
                    .ToList();
            }

            // 🔹 STEP 4 — Expand serial numbers per qty_released
            var groupedData = baseData
                .GroupBy(x => new
                {
                    x.Job,
                    x.OperNum,
                    x.Wc,
                    x.QtyReleased,
                    x.Item,
                    x.CreateDate,
                    x.WcDescription
                })
                .SelectMany(g =>
                    Enumerable.Range(1, (int)g.Key.QtyReleased).Select(i =>
                        new UnpostedTransactionDto
                        {
                            SerialNo = $"{g.Key.Job.Trim()}-{i}",
                            Job = g.Key.Job.Trim(),
                            QtyReleased = 1,
                            Item = g.Key.Item,
                            JobYear = g.Key.CreateDate.Year,
                            OperNum = g.Key.OperNum.ToString(),
                            WcCode = g.Key.Wc,
                            WcDescription = g.Key.WcDescription,
                            EmpNum = string.Join(", ", employees.Where(e => e.Wc == g.Key.Wc).Select(e => e.EmpNum)),
                            EmpName = string.Join(", ", employees.Where(e => e.Wc == g.Key.Wc).Select(e => e.Name))
                        })
                )
                .ToList();

            // 🔹 FINAL DEDUPE – ensures absolutely no double entries
            groupedData = groupedData
                .GroupBy(x => new { x.SerialNo, x.OperNum, x.WcCode })
                .Select(g => g.First())
                .ToList();

            // 🔹 STEP 5 — Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                groupedData = groupedData.Where(x =>
                    x.Job.ToLower().Contains(s)
                    || x.Item.ToLower().Contains(s)
                    || (x.EmpName ?? "").ToLower().Contains(s)
                    || (x.WcDescription ?? "").ToLower().Contains(s)
                ).ToList();
            }

            // 🔹 STEP 6 — Find completed jobs
            var completed = await _context.JobTranMst
                .Where(j => j.SerialNo != null && j.status == "3")
                .Select(j => $"{j.SerialNo}-{j.oper_num}-{j.wc}")
                .ToListAsync();

            var completedSet = new HashSet<string>(completed);

            groupedData = groupedData
                .Where(x => !completedSet.Contains($"{x.SerialNo}-{x.OperNum}-{x.WcCode}"))
                .ToList();

            // 🔹 STEP 7 — Detect active status
            var allSerials = groupedData.Select(x => x.SerialNo).ToList();

            var latestStatuses = await _context.JobTranMst
                .Where(j => allSerials.Contains(j.SerialNo))
                .GroupBy(j => new { j.SerialNo, j.oper_num, j.wc })
                .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault())
                .ToListAsync();

            var statusDict = latestStatuses
                .Where(x => x != null)
                .ToDictionary(
                    x => $"{x!.SerialNo}-{x.oper_num}",
                    x => x!.status
                );

            foreach (var item in groupedData)
            {
                var key = $"{item.SerialNo}-{item.OperNum}";
                item.IsActive = statusDict.TryGetValue(key, out var st) && st != "3";
            }

            // 🔹 STEP 8 — Final sort
            groupedData = groupedData
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Job)
                .ThenBy(x => x.OperNum)
                .ThenBy(x => x.SerialNo)
                .ToList();

            // 🔹 STEP 9 — Pagination
            var totalRecords = groupedData.Count;
            var pagedData = groupedData
                .Skip(page * size)
                .Take(size)
                .ToList();

            return Ok(new
            {
                totalRecords,
                page,
                size,
                data = pagedData
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetVerifyTransactions(
        int page = 0,
        int size = 50,
        string search = "",
        string emp_num = "")
    {
        try
        {
            var fromDate = new DateTime(2025, 11, 1);

            // 🔹 Employee role check
            var currentEmp = await _context.EmployeeMst
                .FirstOrDefaultAsync(e => e.emp_num == emp_num);
            bool isOprLevel4 = currentEmp != null && currentEmp.RoleID == 4;

            // 🔹 STEP 1 — Fetch unique issued job routes
            var baseData = await (
                from jr in _context.JobRouteMst
                join jm in _context.JobMst on jr.Job equals jm.job
                join wm in _context.WcMst on jr.Wc equals wm.wc
                where wm.dept == "OPR"
                && jm.CreateDate >= fromDate
                && jr.Wc.ToLower().Contains("VERIFY")
                select new
                {
                    Job = jr.Job,
                    OperNum = jr.OperNum,
                    Wc = jr.Wc,
                    QtyReleased = jm.qty_released,
                    Item = jm.item,
                    CreateDate = jm.CreateDate,
                    WcDescription = wm.description
                })
                .ToListAsync();

            // ✔ Now remove duplicates purely by Job + Oper + Wc (safe)
            baseData = baseData
                .GroupBy(x => new { x.Job, x.OperNum, x.Wc })
                .Select(g => g.First())
                .ToList();


            // 🔹 STEP 2 — Employee mapping for WCs
            var wcList = baseData.Select(x => x.Wc).Distinct().ToList();
            var employees = await _context.WomWcEmployee
                .Where(e => wcList.Contains(e.Wc))
                .ToListAsync();

            // 🔹 STEP 3 — Role filter (OPR + Level 4 user)
            if (isOprLevel4)
            {
                var allowedWc = employees
                    .Where(e => e.EmpNum == emp_num && e.Wc != null)
                    .Select(e => e.Wc!)
                    .Distinct()
                    .ToList();

                baseData = baseData
                    .Where(x => allowedWc.Contains(x.Wc))
                    .ToList();
            }

            // 🔹 STEP 4 — Expand serial numbers per qty_released
            var groupedData = baseData
                .GroupBy(x => new
                {
                    x.Job,
                    x.OperNum,
                    x.Wc,
                    x.QtyReleased,
                    x.Item,
                    x.CreateDate,
                    x.WcDescription
                })
                .SelectMany(g =>
                    Enumerable.Range(1, (int)g.Key.QtyReleased).Select(i =>
                        new UnpostedTransactionDto
                        {
                            SerialNo = $"{g.Key.Job.Trim()}-{i}",
                            Job = g.Key.Job.Trim(),
                            QtyReleased = 1,
                            Item = g.Key.Item,
                            JobYear = g.Key.CreateDate.Year,
                            OperNum = g.Key.OperNum.ToString(),
                            WcCode = g.Key.Wc,
                            WcDescription = g.Key.WcDescription,
                            EmpNum = string.Join(", ", employees.Where(e => e.Wc == g.Key.Wc).Select(e => e.EmpNum)),
                            EmpName = string.Join(", ", employees.Where(e => e.Wc == g.Key.Wc).Select(e => e.Name))
                        })
                )
                .ToList();

            // 🔹 FINAL DEDUPE – ensures absolutely no double entries
            groupedData = groupedData
                .GroupBy(x => new { x.SerialNo, x.OperNum, x.WcCode })
                .Select(g => g.First())
                .ToList();

            // 🔹 STEP 5 — Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                groupedData = groupedData.Where(x =>
                    x.Job.ToLower().Contains(s)
                    || x.Item.ToLower().Contains(s)
                    || (x.EmpName ?? "").ToLower().Contains(s)
                    || (x.WcDescription ?? "").ToLower().Contains(s)
                ).ToList();
            }

            // 🔹 STEP 6 — Find completed jobs
            var completed = await _context.JobTranMst
                .Where(j => j.SerialNo != null && j.status == "3")
                .Select(j => $"{j.SerialNo}-{j.oper_num}-{j.wc}")
                .ToListAsync();

            var completedSet = new HashSet<string>(completed);

            groupedData = groupedData
                .Where(x => !completedSet.Contains($"{x.SerialNo}-{x.OperNum}-{x.WcCode}"))
                .ToList();

            // 🔹 STEP 7 — Detect active status
            var allSerials = groupedData.Select(x => x.SerialNo).ToList();

            var latestStatuses = await _context.JobTranMst
                .Where(j => allSerials.Contains(j.SerialNo))
                .GroupBy(j => new { j.SerialNo, j.oper_num, j.wc })
                .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault())
                .ToListAsync();

            var statusDict = latestStatuses
                .Where(x => x != null)
                .ToDictionary(
                    x => $"{x!.SerialNo}-{x.oper_num}-{x.wc}",
                    x => new
                    {
                        x.trans_num,
                        x.status,
                        x.Remark
                    }
                );


            foreach (var item in groupedData)
            {
                var key = $"{item.SerialNo}-{item.OperNum}-{item.WcCode}";

                if (statusDict.TryGetValue(key, out var st))
                {
                    item.trans_number = (int)st.trans_num;
                    item.Status = st.status;
                    item.Remark = st.Remark;
                    item.IsActive = st.status != "3";
                }
            }

            // 🔹 STEP 8 — Final sort
            groupedData = groupedData
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Job)
                .ThenBy(x => x.OperNum)
                .ThenBy(x => x.SerialNo)
                .ToList();

            // 🔹 STEP 9 — Pagination
            var totalRecords = groupedData.Count;
            var pagedData = groupedData
                .Skip(page * size)
                .Take(size)
                .ToList();

            return Ok(new
            {
                totalRecords,
                page,
                size,
                data = pagedData
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetCompletedVerifyJob()
    {
        try
        {
            // STEP 1: All VERIFY transactions (any status)
            var allVerifyRows = await _context.JobTranMst
                .Where(j =>
                    j.wc == "VERIFY" &&
                    (j.qty_scrapped == null || j.qty_scrapped == 0))
                .ToListAsync();

            // STEP 2: Groups which HAVE at least one completed row
            var completedGroups = allVerifyRows
                .GroupBy(j => new
                {
                    j.job,
                    j.SerialNo,
                    j.oper_num,
                    j.wc
                })
                .Where(g => g.Any(x => x.status == "3")) // completed job
                .Select(g => new
                {
                    // Latest completed transaction
                    LatestCompleted = g
                        .Where(x => x.status == "3")
                        .OrderByDescending(x => x.trans_date)
                        .First(),

                    // ALL remarks from ANY status
                    Remarks = g
                        .Where(x => !string.IsNullOrWhiteSpace(x.Remark))
                        .Select(x => new
                        {
                            x.emp_num,
                            x.Remark
                        })
                        .Distinct()
                        .ToList()
                })
                .ToList();

            // STEP 3: Employee names
            var empNums = completedGroups
                .SelectMany(g => g.Remarks)
                .Select(e => e.emp_num)
                .Distinct()
                .ToList();

            var empMap = await _context.EmployeeMst
                        .Where(e => empNums.Contains(e.emp_num))
                        .ToDictionaryAsync(
                            e => e.emp_num,
                            e => e.name
                        );


            // STEP 4: Final response
            var result = completedGroups
                .Select(g => new
                {
                    trans_num = g.LatestCompleted.trans_num,
                    job = g.LatestCompleted.job,
                    serialNo = g.LatestCompleted.SerialNo,
                    operNum = g.LatestCompleted.oper_num,
                    wcCode = g.LatestCompleted.wc,
                    transDate = g.LatestCompleted.trans_date,
                    qcGroup = g.LatestCompleted.qcgroup,
                    status = g.LatestCompleted.status,

                    employees = g.Remarks.Select(r => new
                    {
                        empNum = r.emp_num,
                        empName = empMap.ContainsKey(r.emp_num)
                            ? empMap[r.emp_num]
                            : "",
                        remark = r.Remark
                    }).ToList()
                })
                .OrderByDescending(x => x.transDate)
                .ToList();

            return Ok(new { data = result, totalRecords = result.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }




    [HttpGet]
    public async Task<IActionResult> IsNextJobActive()
    {
        try
        {
            var fromDate = new DateTime(2025, 11, 1);

            // 🔹 Step 1: Take latest transaction per Job+Serial+WC+Oper
            var latestTrans = await _context.JobTranMst
                .Where(t =>
                    t.SerialNo != null &&
                    t.trans_date >= fromDate &&
                    t.status == "3"
                )
                .GroupBy(t => new
                {
                    t.job,
                    t.SerialNo,
                    t.wc,
                    t.oper_num
                })
                .Select(g => g
                    .OrderByDescending(x => x.trans_num)
                    .FirstOrDefault()
                )
                .ToListAsync();

            // 🔹 Step 2: Build response
            var result = latestTrans
                .Where(x => x != null && x.next_oper != null)
                .Select(x => new NextJobActiveDto
                {
                    Job = x.job,
                    SerialNo = x.SerialNo,
                    Wc = x.wc,
                    OperNum = (int)x.oper_num,
                    NextOper = x.next_oper,
                    IsNextJobActive = true
                })
                .ToList();

            return Ok(new
            {
                totalRecords = result.Count,
                data = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }



    // [HttpGet]
    // public IActionResult GetActiveJobTransactions()
    // {
    //     // Step 1: group and compute latest + total hours
    //     var grouped = _context.JobTranMst
    //         .GroupBy(j => new { j.job, j.SerialNo, j.oper_num, j.wc })
    //         .Select(g => new
    //         {
    //             Latest = g.OrderByDescending(x => x.trans_date).FirstOrDefault(),
    //             TotalHours = g.Sum(x => x.a_hrs ?? 0)
    //         })
    //         .ToList(); // run on SQL first, then filter in memory safely

    //     // Step 2: filter only active jobs (status 1 or 2)
    //     var latestJobs = grouped
    //         .Where(x => x.Latest != null && (x.Latest.status == "1" || x.Latest.status == "2"))
    //         .Select(x => new
    //         {
    //             x.Latest!.job,
    //             x.Latest.SerialNo,
    //             x.Latest.oper_num,
    //             x.Latest.wc,
    //             x.Latest.start_time,
    //             x.Latest.end_time,
    //             x.Latest.status,
    //             x.Latest.emp_num,
    //             x.Latest.machine_id,
    //             a_hrs = x.TotalHours
    //         })
    //         .ToList();

    //     return Ok(new { data = latestJobs, total = latestJobs.Count });
    // }

    [HttpGet]
public IActionResult GetActiveJobTransactions()
{
    var latestByJob = _context.JobTranMst
        .Where(j =>
            j.trans_type == "D" &&
            (j.reopen_flag == null || j.reopen_flag == false)  // ← explicit
        )
        .GroupBy(j => new { j.job, j.SerialNo, j.oper_num, j.wc })
        .Select(g => new
        {
            Latest     = g.OrderByDescending(x => x.trans_date).FirstOrDefault(),
            TotalHours = g.Where(x => x.status == "1").Sum(x => x.a_hrs ?? 0)
        })
        .ToList();
 
    var activeJobs = latestByJob
        .Where(x => x.Latest != null &&
                    (x.Latest.status == "1" || x.Latest.status == "2"))
        .Select(x => new
        {
            x.Latest!.job,
            x.Latest.SerialNo,
            x.Latest.oper_num,
            x.Latest.wc,
            x.Latest.start_time,
            x.Latest.end_time,
            x.Latest.status,
            x.Latest.emp_num,
            emp_name = _context.EmployeeMst
                        .Where(e => e.emp_num == x.Latest.emp_num)
                        .Select(e => e.name)
                        .FirstOrDefault(),
            x.Latest.machine_id,
            total_a_hrs = x.TotalHours
        })
        .ToList();
 
    return Ok(new { data = activeJobs, total = activeJobs.Count });
}



    [HttpGet]
    public async Task<IActionResult> CanStartJob(string job, string serialNo, int operNum)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(job) || string.IsNullOrWhiteSpace(serialNo) || operNum <= 0)
                return BadRequest(new { success = false, message = "Invalid input.", canStart = false });

            // Fetch operations for this job
            var jobOps = await _context.JobRouteMst
                .Where(j => (j.Job ?? string.Empty).Trim() == job.Trim())
                .Select(j => j.OperNum)
                .Distinct()
                .OrderBy(o => o)
                .ToListAsync();

            if (!jobOps.Any())
                return NotFound(new { success = false, message = "Job not found in route.", canStart = false });

            int index = jobOps.IndexOf(operNum);
            if (index == -1)
                return BadRequest(new { success = false, message = $"Operation {operNum} not part of job {job}.", canStart = false });

            // Get latest transaction status for this serial only
            var latestStatuses = await _context.JobTranMst
                .Where(jt => jt.job == job && jt.SerialNo == serialNo)
                .GroupBy(jt => jt.oper_num)
                .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault())
                .ToListAsync();

            var statusDict = latestStatuses
                .Where(x => x != null && x.oper_num.HasValue)
                .ToDictionary(x => x!.oper_num!.Value, x => x!.status);

            // ✅ Only check previous operation for THIS serial
            if (index > 0)
            {
                var prevOp = jobOps[index - 1];
                if (!statusDict.TryGetValue(prevOp, out var prevStatus) || (prevStatus != "2" && prevStatus != "3"))
                {
                    return Ok(new { success = false, message = $"Previous operation ({prevOp}) of this serial must be completed/paused first.", canStart = false });
                }
            }

            // ✅ Passed all checks → Can start
            return Ok(new { success = true, message = $"Job {job}, Serial {serialNo}, Operation {operNum} can be started.", canStart = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error while validating job start.", error = ex.Message, canStart = false });
        }
    }








    // [HttpGet] public async Task<IActionResult> GetUnpostedTransactions( int page = 0, int size = 50, string search = "", string emp_num = "") { try { var fromDate = new DateTime(2025, 8, 1); // Step 0: Check logged-in employee info var currentEmp = await _context.EmployeeMst .FirstOrDefaultAsync(e => e.emp_num == emp_num); bool isOprLevel4 = currentEmp != null && currentEmp.RoleID == 4; // Step 1: Fetch the base data from DB asynchronously var baseData = await ( from jr in _context.JobRouteMst join jm in _context.JobMst on jr.Job equals jm.job join wm in _context.WcMst on jr.Wc equals wm.wc where wm.dept == "OPR" && jm.CreateDate >= fromDate select new { jr, jm, wm }) .ToListAsync(); // Step 2: Fetch related employees for the WCs var wcList = baseData.Select(x => x.jr.Wc).Distinct().ToList(); var employees = await _context.WomWcEmployee .Where(e => wcList.Contains(e.Wc)) .ToListAsync(); // ✅ Step 3: If this user is OPR+roleID4, only allow jobs of this emp_num List<string> allowedWcForCurrentEmp = new(); if (isOprLevel4) { allowedWcForCurrentEmp = employees .Where(e => e.EmpNum == emp_num) .Select(e => e.Wc) .Distinct() .ToList(); // Filter baseData to only WCs where this emp is assigned baseData = baseData .Where(x => allowedWcForCurrentEmp.Contains(x.jr.Wc)) .ToList(); } // Step 4: Group by job/oper/wc and aggregate employees var groupedData = baseData .GroupBy(x => new { x.jr.Job, x.jr.OperNum, x.jr.Wc, x.jm.qty_released, x.jm.item, x.jm.CreateDate, x.wm.description }) .SelectMany(g => { var empNums = string.Join(", ", employees.Where(e => e.Wc == g.Key.Wc).Select(e => e.EmpNum)); var empNames = string.Join(", ", employees.Where(e => e.Wc == g.Key.Wc).Select(e => e.Name)); return Enumerable.Range(1, (int)g.Key.qty_released).Select(i => new UnpostedTransactionDto { SerialNo = $"{(g.Key.Job ?? "").Trim()}-{i}", Job = (g.Key.Job ?? "").Trim(), QtyReleased = 1, Item = g.Key.item ?? "", JobYear = g.Key.CreateDate.Year, OperNum = g.Key.OperNum.ToString(), WcCode = g.Key.Wc ?? "", WcDescription = g.Key.description ?? "", EmpNum = empNums, EmpName = empNames }); }) .OrderBy(x => x.Job) .ThenBy(x => x.OperNum) .ThenBy(x => x.SerialNo) .ToList(); // Step 5: Optional search if (!string.IsNullOrWhiteSpace(search)) { var lowerSearch = search.ToLower(); groupedData = groupedData .Where(x => x.Job.ToLower().Contains(lowerSearch) || x.Item.ToLower().Contains(lowerSearch) || (x.EmpName ?? "").ToLower().Contains(lowerSearch) || (x.MachineDescription ?? "").ToLower().Contains(lowerSearch)) .ToList(); } // Step 6: Pagination var totalRecords = groupedData.Count; var pagedData = groupedData .Skip(page * size) .Take(size) .ToList(); return Ok(new { totalRecords, page, size, data = pagedData }); } catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); } }


    // [HttpGet]
    // public async Task<IActionResult> GetQC(int page = 0, int size = 50, string search = "")
    // {
    //     try
    //     {
    //         var fromDate = new DateTime(2025, 7, 1);

    //         // Step 1: Get jobs from JobTranMst with status = 3 AND QC GROUP BLANK
    //         var jobTranJobs = await _context.JobTranMst
    //             .Where(t => t.status == "3" && (t.qcgroup == null || t.qcgroup == ""))
    //             .Select(t => t.job)
    //             .Distinct()
    //             .ToListAsync();

    //         // Step 2: Get JobMst data
    //         var jobMstData = await _context.JobMst
    //             .Where(jm => jobTranJobs.Contains(jm.job) && jm.CreateDate >= fromDate)
    //             .Select(jm => new { jm.job, jm.qty_released, jm.item, jm.CreateDate })
    //             .ToListAsync();

    //         // Step 3: JobRouteMst for QA/QC operations
    //         var jobRouteData = await (
    //             from jr in _context.JobRouteMst
    //             join wm in _context.WcMst on jr.Wc equals wm.wc
    //             where wm.dept == "QA/ QC" && jobTranJobs.Contains(jr.Job)
    //             // where wm.wc.StartsWith("Q") && jobTranJobs.Contains(jr.Job)
    //             select new { jr.Job, jr.OperNum, jr.Wc, wm.description }
    //         ).ToListAsync();

    //         // Step 4: Merge JobMst + JobRouteMst
    //         var baseData = (
    //             from jm in jobMstData
    //             join jr in jobRouteData on jm.job equals jr.Job
    //             select new
    //             {
    //                 Job = jm.job,
    //                 jm.qty_released,
    //                 jm.item,
    //                 jm.CreateDate,
    //                 jr.OperNum,
    //                 jr.Wc,
    //                 jr.description
    //             }).ToList();

    //         // Step 5: Fetch employees for WC
    //         var wcList = baseData.Select(x => x.Wc).Distinct().ToList();
    //         var employees = await _context.WomWcEmployee
    //             .Where(e => wcList.Contains(e.Wc))
    //             .ToListAsync();

    //         //  Step 6: Fetch already completed QC serials (where qcgroup is not null or empty)
    //         var completedQcSerials = await _context.JobTranMst
    //             .Where(t => !string.IsNullOrEmpty(t.SerialNo) &&
    //                         !string.IsNullOrWhiteSpace(t.qcgroup) &&
    //                         jobTranJobs.Contains(t.job))
    //             .Select(t => t.SerialNo.Trim())
    //             .Distinct()
    //             .ToListAsync();

    //         // Step 7: Build final DTO excluding completed serials
    //         var finalData = baseData
    //             .SelectMany(bd =>
    //             {
    //                 // Generate serials and skip completed ones
    //                 return Enumerable.Range(1, (int)bd.qty_released)
    //                     .Select(i => $"{bd.Job.Trim()}-{i}")
    //                     .Where(serial => !completedQcSerials.Contains(serial)) // skip completed ones
    //                     .Select(serial =>
    //                     {
    //                         var empList = employees.Where(e => e.Wc == bd.Wc).ToList();
    //                         var empNums = string.Join(", ", empList.Select(e => e.EmpNum));
    //                         var empNames = string.Join(", ", empList.Select(e => e.Name));

    //                         return new UnpostedTransactionDto
    //                         {
    //                             SerialNo = serial,
    //                             Job = bd.Job.Trim(),
    //                             QtyReleased = 1,
    //                             Item = bd.item ?? "",
    //                             JobYear = bd.CreateDate.Year,
    //                             OperNum = bd.OperNum.ToString(),
    //                             WcCode = bd.Wc,
    //                             WcDescription = bd.description,
    //                             EmpNum = empNums,
    //                             EmpName = empNames,
    //                             trans_number = 0,
    //                             TransType = "",
    //                             QcGroup = "",
    //                             Status = "",   // QC not started
    //                             MachineId = "",
    //                             MachineDescription = "",
    //                             IsActive = false,
    //                         };
    //                     });
    //             })
    //             .OrderBy(x => x.Job)
    //             .ThenBy(x => x.OperNum)
    //             .ThenBy(x => x.SerialNo)
    //             .ToList();

    //         // Step 8: Apply search
    //         if (!string.IsNullOrWhiteSpace(search))
    //         {
    //             var lower = search.ToLower();
    //             finalData = finalData
    //                 .Where(x =>
    //                     x.Job.ToLower().Contains(lower) ||
    //                     x.Item.ToLower().Contains(lower) ||
    //                     (x.EmpName ?? "").ToLower().Contains(lower))
    //                 .ToList();
    //         }

    //         // Step 9: Pagination
    //         var totalRecords = finalData.Count;
    //         var pagedData = finalData.Skip(page * size).Take(size).ToList();

    //         return Ok(new { totalRecords, page, size, data = pagedData });
    //     }
    //     catch (Exception ex)
    //     {
    //         return StatusCode(500, new { error = ex.Message, innerError = ex.InnerException?.Message });
    //     }
    // }



   [HttpGet]
    public async Task<IActionResult> GetQC(int page = 0, int size = 50, string search = "")
    {
        try
        {
            var fromDate = new DateTime(2025, 11, 1);
    
            // Step 1: Get all QA/QC operations
            var qcOperations = await (
                from jr in _context.JobRouteMst
                join wm in _context.WcMst on jr.Wc equals wm.wc
                where wm.dept == "QA/ QC"
                select new { jr.Job, jr.OperNum, jr.Wc, wm.description }
            ).ToListAsync();
    
            var jobList = qcOperations.Select(x => x.Job).Distinct().ToList();
    
            // Step 2: Get JobMst data for all these jobs
            var jobMstData = await _context.JobMst
                .Where(jm => jobList.Contains(jm.job) && jm.CreateDate >= fromDate)
                .Select(jm => new { jm.job, jm.qty_released, jm.item, jm.CreateDate })
                .ToListAsync();
    
            // Step 3: Merge JobMst + all QC routes
            var baseData = (
                from jm in jobMstData
                join jr in qcOperations on jm.job equals jr.Job
                select new
                {
                    Job          = jm.job,
                    jm.qty_released,
                    jm.item,
                    jm.CreateDate,
                    jr.OperNum,
                    jr.Wc,
                    jr.description
                }).ToList();
    
            // Step 4: Fetch employees for WC
            var wcList = baseData.Select(x => x.Wc).Distinct().ToList();
            var employees = await _context.WomWcEmployee
                .Where(e => wcList.Contains(e.Wc))
                .ToListAsync();
    
            // Step 5: Fetch completed QC serials
            var completedQcSerials = await _context.JobTranMst
                .Where(t =>
                    !string.IsNullOrEmpty(t.SerialNo) &&
                    t.status == "3" &&
                    t.trans_type == "M" &&
                    jobList.Contains(t.job))
                .Select(t => new { Serial = t.SerialNo.Trim(), OperNum = t.oper_num })
                .Distinct()
                .ToListAsync();
    
            // ── FIX: fetch reopen-waiting QC rows (status="2" + reopen_flag=true)
            // These override completed — job was reopened so show in New Jobs tab again
            var reopenedQcRows = await _context.JobTranMst
                .Where(t =>
                    !string.IsNullOrEmpty(t.SerialNo) &&
                    t.status == "2" &&
                    t.reopen_flag == true &&
                    t.trans_type == "M" &&
                    jobList.Contains(t.job))
                .Select(t => new { Serial = t.SerialNo.Trim(), OperNum = t.oper_num })
                .ToListAsync();
    
            // Build reopen set — these serials/opers should NOT be excluded
            var reopenedQcSet = new HashSet<string>(
                reopenedQcRows.Select(x => $"{x.Serial}-{x.OperNum}")
            );
    
            // Step 6: Build final DTO
            // Exclude completed serials UNLESS they have been reopened
            var finalData = baseData
                .SelectMany(bd =>
                {
                    return Enumerable.Range(1, (int)bd.qty_released)
                        .Select(i => $"{bd.Job.Trim()}-{i}")
                        .Where(serial =>
                        {
                            var key = $"{serial}-{bd.OperNum}";
                            // If reopened → include (override completed filter)
                            if (reopenedQcSet.Contains(key)) return true;
                            // If completed → exclude
                            if (completedQcSerials.Any(x => x.Serial == serial && x.OperNum == bd.OperNum))
                                return false;
                            return true;
                        })
                        .Select(serial =>
                        {
                            var empList  = employees.Where(e => e.Wc == bd.Wc).ToList();
                            var empNums  = string.Join(", ", empList.Select(e => e.EmpNum));
                            var empNames = string.Join(", ", empList.Select(e => e.Name));
    
                            return new UnpostedTransactionDto
                            {
                                SerialNo         = serial,
                                Job              = bd.Job.Trim(),
                                QtyReleased      = 1,
                                Item             = bd.item ?? "",
                                JobYear          = bd.CreateDate.Year,
                                OperNum          = bd.OperNum.ToString(),
                                WcCode           = bd.Wc,
                                WcDescription    = bd.description,
                                EmpNum           = empNums,
                                EmpName          = empNames,
                                trans_number     = 0,
                                TransType        = "",
                                QcGroup          = "",
                                Status           = "",
                                MachineId        = "",
                                MachineDescription = "",
                                IsActive         = false,
                            };
                        });
                })
                .OrderBy(x => x.Job)
                .ThenBy(x => x.OperNum)
                .ThenBy(x => x.SerialNo)
                .ToList();
    
            // Step 7: Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                finalData = finalData
                    .Where(x =>
                        x.Job.ToLower().Contains(lower) ||
                        x.Item.ToLower().Contains(lower) ||
                        (x.EmpName ?? "").ToLower().Contains(lower))
                    .ToList();
            }
    
            // Step 8: Pagination
            var totalRecords = finalData.Count;
            var pagedData    = finalData.Skip(page * size).Take(size).ToList();
    
            return Ok(new { totalRecords, page, size, data = pagedData });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, innerError = ex.InnerException?.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetOnlyQCJobs(int page = 0, int size = 50, string search = "")
    {
        try
        {
            var fromDate = new DateTime(2025, 7, 1);

            // Step 1: Get jobs where ALL operations are in QA/ QC
            var qcOnlyJobs = await (
                from jr2 in _context.JobRouteMst
                join w2 in _context.WcMst on jr2.Wc equals w2.wc
                group w2 by jr2.Job into g
                where g.Select(x => x.dept).Distinct().Count() == 1
                && g.Max(x => x.dept) == "QA/ QC"
                select g.Key
            ).ToListAsync();

            if (!qcOnlyJobs.Any())
                return Ok(new { totalRecords = 0, page, size, data = new List<object>() });

            // Step 2: JobMst for those jobs
            var jobMstData = await _context.JobMst
                .Where(jm => qcOnlyJobs.Contains(jm.job) && jm.CreateDate >= fromDate)
                .Select(jm => new { jm.job, jm.qty_released, jm.item, jm.CreateDate })
                .ToListAsync();

            // Step 3: JobRouteMst + WC (only QA/ QC jobs, but keep WC info)
            var jobRouteData = await (
                from jr in _context.JobRouteMst
                join wm in _context.WcMst on jr.Wc equals wm.wc
                where qcOnlyJobs.Contains(jr.Job)
                select new { jr.Job, jr.OperNum, jr.Wc, wm.description, wm.dept }
            ).ToListAsync();

            // Step 4: Merge JobMst + JobRoute
            var baseData = (
                from jm in jobMstData
                join jr in jobRouteData on jm.job equals jr.Job
                select new
                {
                    Job = jm.job,
                    jm.qty_released,
                    jm.item,
                    jm.CreateDate,
                    jr.OperNum,
                    jr.Wc,
                    jr.description,
                    jr.dept
                }).ToList();

            // Step 5: Serial number expansion (similar to your GetQC)
            var finalData = baseData
                .SelectMany(bd =>
                {
                    return Enumerable.Range(1, (int)bd.qty_released).Select(i => new
                    {
                        SerialNo = $"{bd.Job.Trim()}-{i}",
                        Job = bd.Job.Trim(),
                        QtyReleased = 1,
                        Item = bd.item ?? "",
                        JobYear = bd.CreateDate.Year,
                        OperNum = bd.OperNum.ToString(),
                        WcCode = bd.Wc,
                        WcDescription = bd.description,
                        Dept = bd.dept
                    });
                })
                .OrderBy(x => x.Job)
                .ThenBy(x => x.OperNum)
                .ThenBy(x => x.SerialNo)
                .ToList();

            // Step 6: Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                finalData = finalData
                    .Where(x =>
                        x.Job.ToLower().Contains(lower) ||
                        x.Item.ToLower().Contains(lower) ||
                        (x.WcDescription ?? "").ToLower().Contains(lower))
                    .ToList();
            }

            // Step 7: Pagination
            var totalRecords = finalData.Count;
            var pagedData = finalData.Skip(page * size).Take(size).ToList();

            return Ok(new { totalRecords, page, size, data = pagedData });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, innerError = ex.InnerException?.Message });
        }
    }







    [HttpGet("{jobNumber}/{operationNumber}")]
    public async Task<IActionResult> GetEmployeesForJob(string jobNumber, string operationNumber)
    {
        try
        {
            if (!int.TryParse(operationNumber, out var operNum))
                return BadRequest("Invalid operation number");

            var wcList = await _context.JobRouteMst
            .Where(jr => jr.Job == jobNumber && jr.OperNum == operNum)
            .Select(jr => jr.Wc)
            .Distinct()
            .ToListAsync();

            if (!wcList.Any())
                return Ok(new List<EmployeeGetDto>());

            // 2. Get all employees from these WCs
            var employees = await _context.WomWcEmployee
                .Where(e => wcList.Contains(e.Wc))
                .Select(e => new EmployeeGetDto
                {
                    EmpNum = e.EmpNum,
                    Name = e.Name ?? ""
                })
                .ToListAsync();

            return Ok(employees);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{jobNumber}/{operationNumber}")]
    public async Task<IActionResult> GetMachinesForJob(string jobNumber, string operationNumber)
    {
        try
        {
            if (!int.TryParse(operationNumber, out var operNum))
                return BadRequest("Invalid operation number");

            var wcList = await _context.JobRouteMst
            .Where(jr => jr.Job == jobNumber && jr.OperNum == operNum)
            .Select(jr => jr.Wc)
            .Distinct()
            .ToListAsync();

            if (!wcList.Any())
                return Ok(new List<MachineGetDto>());

            // 2. Get all machines from these WCs
            var machines = await _context.WomWcMachines
                .Where(m => wcList.Contains(m.Wc))
                .Select(m => new MachineGetDto
                {
                    MachineId = m.MachineId,
                    MachineDescription = m.MachineDescription
                })
                .ToListAsync();

            return Ok(machines);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }



    [HttpGet]
public IActionResult GetActiveQCJobs()
{
    var latestByJob = _context.JobTranMst
        .Where(j => j.trans_type == "M") // ✅ NO reopen filter here
        .GroupBy(j => new { j.job, j.SerialNo, j.oper_num, j.wc })
        .Select(g => new
        {
            Latest = g
                .OrderByDescending(x => x.trans_date)
                .ThenByDescending(x => x.trans_num) // 🔥 IMPORTANT FIX
                .FirstOrDefault(),

            TotalHours = g
                .Where(x => x.status == "1")
                .Sum(x => x.a_hrs ?? 0)
        })
        .ToList();

    var activeJobs = latestByJob
        .Where(x =>
            x.Latest != null &&
            (
                x.Latest.status == "1" || 
                (
                    x.Latest.status == "2" &&
                    (x.Latest.reopen_flag == null || x.Latest.reopen_flag == false)
                )
            )
        )
        .Select(x => new
        {
            x.Latest!.job,
            x.Latest.SerialNo,
            x.Latest.oper_num,
            x.Latest.wc,
            x.Latest.start_time,
            x.Latest.end_time,
            x.Latest.status,
            x.Latest.emp_num,
            emp_name = _context.EmployeeMst
                        .Where(e => e.emp_num == x.Latest.emp_num)
                        .Select(e => e.name)
                        .FirstOrDefault(),
            x.Latest.machine_id,
            total_a_hrs = x.TotalHours
        })
        .ToList();

    return Ok(new { data = activeJobs, total = activeJobs.Count });
}
    

    [HttpGet]
    public async Task<IActionResult> GetEmployeeList()
    {
        try
        {
            var employees = await _context.EmployeeMst
                .Select(e => new EmployeeDtos
                {
                    EmpNum = e.emp_num,
                    Name = e.name
                })
                .ToListAsync();

            return Ok(new { data = employees, total = employees.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching employees", error = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetCompletedQCJobs()
    {
        try
        {
            // Fetch all completed jobs asynchronously
            var completedJobs = await _context.JobTranMst
                .Where(j => j.status == "3" && j.trans_type == "M" && (j.qty_scrapped == null || j.qty_scrapped == 0))
                .ToListAsync();

            // Group by Job/Operation/Serial/WC and select latest completed
            var grouped = completedJobs
                .GroupBy(j => new { j.job, j.oper_num, j.SerialNo, j.qcgroup })
                .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault())
                .OrderByDescending(j => j!.trans_date)
                .ToList();

            // Map to DTO with data from JobMst
            var result = new List<object>();
            foreach (var j in grouped)
            {
                var jobMaster = await _context.JobMst
                    .FirstOrDefaultAsync(jm => jm.job == j!.job);

                result.Add(new
                {
                    trans_num = j.trans_num,
                    jobNumber = j!.job,
                    serialNo = j.SerialNo,
                    operationNumber = j.oper_num,
                    wcCode = j.wc,
                    empNum = j.emp_num,
                    emp_name = _context.EmployeeMst
                        .Where(e => e.emp_num == j.emp_num)
                        .Select(e => e.name)
                        .FirstOrDefault(),
                    qtyReleased = jobMaster?.qty_released ?? 0,
                    item = jobMaster?.item ?? "",
                    remark = j.completed_comment ?? "",
                    total_a_hrs = j.a_hrs,
                    //  endTime = DateTime.UtcNow // current time for UI
                });
            }

            return Ok(new { data = result, totalRecords = result.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }



   [HttpGet]
    public async Task<IActionResult> GetHoldQCJobs()
    {
        try
        {
            var holdJobs = await _context.JobTranMst
                .Where(j => j.status == "4")
                .ToListAsync();

            var grouped = holdJobs
                .GroupBy(j => new { j.job, j.oper_num, j.SerialNo, j.qcgroup });

            // Fetch ALL statuses for these serials to sum only status=1 hours
            var allSerials = holdJobs.Select(j => j.SerialNo).Distinct().ToList();
            var allRows = await _context.JobTranMst
                .Where(j => allSerials.Contains(j.SerialNo))
                .ToListAsync();

            var result = new List<object>();

            foreach (var g in grouped)
            {
                var latest = g.OrderByDescending(x => x.trans_date).First();

                // Sum ONLY status=1 (active/running) rows for true worked hours
                var totalHours = allRows
                    .Where(x =>
                        x.job == latest.job &&
                        x.SerialNo == latest.SerialNo &&
                        x.oper_num == latest.oper_num &&
                        x.qcgroup == latest.qcgroup &&
                        x.status == "1")
                    .Sum(x => x.a_hrs ?? 0);

                var jobMaster = await _context.JobMst
                    .FirstOrDefaultAsync(jm => jm.job == latest.job);

                result.Add(new
                {
                    trans_num       = latest.trans_num,
                    jobNumber       = latest.job,
                    serialNo        = latest.SerialNo,
                    operationNumber = latest.oper_num,
                    wcCode          = latest.wc,
                    empNum          = latest.emp_num,
                    emp_name        = _context.EmployeeMst
                                        .Where(e => e.emp_num == latest.emp_num)
                                        .Select(e => e.name)
                                        .FirstOrDefault(),
                    qtyReleased     = jobMaster?.qty_released ?? 0,
                    item            = jobMaster?.item ?? "",
                    remark          = latest.hold_comment ?? "",
                    total_a_hrs     = totalHours
                });
            }

            result = result
                .OrderByDescending(r => ((dynamic)r).trans_num)
                .Cast<object>()
                .ToList();

            return Ok(new { data = result, totalRecords = result.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetRejectedQCJobs()
    {
        try
        {
            var rejectedJobs = await _context.JobTranMst
                .Where(j => j.status == "5")
                .ToListAsync();

            var grouped = rejectedJobs
                .GroupBy(j => new { j.job, j.oper_num, j.SerialNo, j.qcgroup });

            // Fetch ALL statuses for these serials to sum only status=1 hours
            var allSerials = rejectedJobs.Select(j => j.SerialNo).Distinct().ToList();
            var allRows = await _context.JobTranMst
                .Where(j => allSerials.Contains(j.SerialNo))
                .ToListAsync();

            var result = new List<object>();

            foreach (var g in grouped)
            {
                var latest = g.OrderByDescending(x => x.trans_date).First();

                // Sum ONLY status=1 (active/running) rows for true worked hours
                var totalHours = allRows
                    .Where(x =>
                        x.job == latest.job &&
                        x.SerialNo == latest.SerialNo &&
                        x.oper_num == latest.oper_num &&
                        x.qcgroup == latest.qcgroup &&
                        x.status == "1")
                    .Sum(x => x.a_hrs ?? 0);

                var jobMaster = await _context.JobMst
                    .FirstOrDefaultAsync(jm => jm.job == latest.job);

                result.Add(new
                {
                    trans_num       = latest.trans_num,
                    jobNumber       = latest.job,
                    serialNo        = latest.SerialNo,
                    operationNumber = latest.oper_num,
                    wcCode          = latest.wc,
                    empNum          = latest.emp_num,
                    emp_name        = _context.EmployeeMst
                                        .Where(e => e.emp_num == latest.emp_num)
                                        .Select(e => e.name)
                                        .FirstOrDefault(),
                    qtyReleased     = jobMaster?.qty_released ?? 0,
                    item            = jobMaster?.item ?? "",
                    remark          = latest.reject_comment ?? "",
                    total_a_hrs     = totalHours
                });
            }

            result = result
                .OrderByDescending(r => ((dynamic)r).trans_num)
                .Cast<object>()
                .ToList();

            return Ok(new { data = result, totalRecords = result.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetScrappedQCJobs()
    {
        try
        {
            // Fetch all scrapped jobs (status = 3, trans_type = M, qty_scrapped = 1)
            var scrappedJobs = await _context.JobTranMst
                .Where(j => j.status == "3" && j.trans_type == "M" && j.qty_scrapped == 1)
                .ToListAsync();

            // Group by Job/Operation/Serial/WC and select the latest scrapped entry
            var grouped = scrappedJobs
                .GroupBy(j => new { j.job, j.oper_num, j.SerialNo, j.qcgroup })
                .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault())
                .OrderByDescending(j => j!.trans_date)
                .ToList();

            // Map to DTO with data from JobMst
            var result = new List<object>();
            foreach (var j in grouped)
            {
                var jobMaster = await _context.JobMst
                    .FirstOrDefaultAsync(jm => jm.job == j!.job);

                result.Add(new
                {
                    jobNumber = j!.job,
                    serialNo = j.SerialNo,
                    operationNumber = j.oper_num,
                    wcCode = j.wc,
                    empNum = j.emp_num,
                    emp_name = _context.EmployeeMst
                        .Where(e => e.emp_num == j.emp_num)
                        .Select(e => e.name)
                        .FirstOrDefault(),
                    qtyReleased = jobMaster?.qty_released ?? 0,
                    item = jobMaster?.item ?? "",
                    qtyScrapped = j.qty_scrapped ?? 0,
                    remark = j.Remark ?? ""
                });
            }

            return Ok(new { data = result, totalRecords = result.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetEmployeeAndMachineStats()
    {
        try
        {
            // ===== Total Employees =====
            int totalEmployees = await _context.EmployeeMst
                .Where(e => e.IsActive == true || e.IsActive == null)
                .CountAsync();

            // ===== Latest job per employee =====
            var latestEmpStatus = await _context.JobTranMst
                .Where(j => j.emp_num != null)
                .GroupBy(j => j.emp_num)
                .Select(g => g.OrderByDescending(x => x.CreateDate).FirstOrDefault())
                .ToListAsync();

            int activeEmployees = latestEmpStatus.Count(j =>
            {
                if (int.TryParse(j.status, out int status))
                    return status == 1;
                return false;
            });

            double activeEmployeePercent = totalEmployees > 0
                ? Math.Round((double)activeEmployees / totalEmployees * 100, 2)
                : 0;
            // ===== Total Machines =====
            int totalMachines = await _context.MachineMaster.CountAsync();

            // ===== Latest job per machine =====
            var latestMachineStatus = await _context.JobTranMst
                .Where(j => j.machine_id != null)
                .GroupBy(j => j.machine_id)
                .Select(g => g.OrderByDescending(x => x.CreateDate).FirstOrDefault())
                .ToListAsync();

            int activeMachines = latestMachineStatus.Count(j =>
            {
                if (int.TryParse(j.status, out int status))
                    return status == 1;
                return false;
            });

            double activeMachinePercent = totalMachines > 0
                ? Math.Round((double)activeMachines / totalMachines * 100, 2)
                : 0;

            // ===== Final Output =====
            var result = new
            {
                TotalEmployees = totalEmployees,
                ActiveEmployees = activeEmployees,
                EmployeeUtilization = $"{activeEmployeePercent}%",
                TotalMachines = totalMachines,
                ActiveMachines = activeMachines,
                MachineUtilization = $"{activeMachinePercent}%"
            };

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Error fetching employee and machine utilization data",
                error = ex.Message
            });
        }
    }

    [HttpGet]

    public async Task<IActionResult> getJobProgress()

    {
        // ===================== DATE FILTER =====================

        DateTime cutoffDate = new DateTime(2025, 12, 07);

        // ===================== JOB MASTER =====================

        var jobs = await _context.JobMst

            .Where(j => j.RecordDate > cutoffDate)

            .Select(j => new JobDtos

            {

                JobNo = j.job,

                Item = j.item,

                Description = j.description,

                Qty = j.qty_released,
                JobDate = j.job_date,

                Serials = new List<JobSerialDto>()

            })

            .AsNoTracking()

            .ToListAsync();

        if (!jobs.Any())

            return Ok(jobs);

        var jobNos = jobs.Select(j => j.JobNo).ToList();

        // ===================== JOB ROUTES =====================

        var jobRoutes = await _context.JobRouteMst

            .Where(r => jobNos.Contains(r.Job))

            .GroupBy(r => r.Job)

            .Select(g => new

            {

                JobNo = g.Key,

                TotalOperations = g.Count(),

                Operations = g.Select(x => new
                {
                    OperNum = x.OperNum,
                    WC = x.Wc   // ⭐ ADD THIS (your WOMMID)
                }).ToList()


            })

            .AsNoTracking()

            .ToListAsync();

        var routeDict = jobRoutes.ToDictionary(r => r.JobNo);

        // ===================== JOB TRANSACTIONS =====================

        var jobTrans = await _context.JobTranMst

            .Where(t => jobNos.Contains(t.job))

            .Select(t => new

            {

                t.job,

                t.SerialNo,

                t.oper_num,

                t.emp_num,

                t.start_time,

                t.end_time,

                t.a_hrs,

                t.status,

                t.RecordDate,
                t.trans_date

            })

            .AsNoTracking()

            .ToListAsync();

        // ===================== EMPLOYEE MASTER =====================

        var empNums = jobTrans

            .Where(t => t.emp_num != null)

            .Select(t => t.emp_num)

            .Distinct()

            .ToList();

        var empDict = await _context.EmployeeMst

            .Where(e => empNums.Contains(e.emp_num))

            .Select(e => new

            {

                e.emp_num,

                e.name

            })

            .AsNoTracking()

            .ToDictionaryAsync(e => e.emp_num, e => e.name);

        // ===================== LOOKUP =====================

        var tranLookup = jobTrans.ToLookup(t => t.SerialNo);

        // ===================== BUILD TREE =====================

        foreach (var job in jobs)

        {

            routeDict.TryGetValue(job.JobNo, out var route);

            for (int i = 1; i <= job.Qty; i++)

            {

                string serialNo = $"{job.JobNo}-{i}";

                var serialTrans = tranLookup[serialNo];

                var serial = new JobSerialDto

                {

                    SerialNo = serialNo,

                    TotalOperations = route?.TotalOperations ?? 0,

                    CompletedOperations = 0,

                    RunningOperations = 0,

                    HoldOperations = 0,

                    // ✅ SERIAL TOTAL HOURS (STATUS = 3)

                    TotalHours = serialTrans

                        .Where(t => t.status == "3")

                        .Sum(t => t.a_hrs),

                    Operations = new List<JobOperationDtos>()

                };

                foreach (var op in route?.Operations ?? Enumerable.Empty<dynamic>())


                {

                    var opTrans = serialTrans
    .Where(t => t.oper_num == op.OperNum)
    .ToList();


                    JobOperationDtos opDto = new JobOperationDtos

                    {

                        Operation = op.OperNum,
                        WC = op.WC


                    };

                    if (opTrans.Any())

                    {

                        var latestTran = opTrans

                           .OrderByDescending(t => t.trans_date)

                            .First();

                        opDto.Employee = latestTran.emp_num != null && empDict.ContainsKey(latestTran.emp_num)

                            ? empDict[latestTran.emp_num]

                            : null;

                        opDto.StartTime = latestTran.start_time?.ToString("HH:mm") ?? "-";

                        opDto.EndTime = latestTran.end_time?.ToString("HH:mm") ?? "-";

                        opDto.HoursConsumed = latestTran.a_hrs;

                        switch (latestTran.status)

                        {

                            case "1":

                                serial.RunningOperations++;

                                opDto.Status = "Running";

                                break;

                            case "2":
                                serial.RunningOperations++;
                                serial.HoldOperations++;

                                opDto.Status = "Pause";

                                break;

                            case "3":

                                serial.CompletedOperations++;

                                opDto.Status = "Completed";

                                break;

                            default:

                                opDto.Status = "Unknown";

                                break;

                        }

                    }

                    else

                    {

                        opDto.Status = "No Transaction Yet";

                        opDto.HoursConsumed = 0;

                        opDto.StartTime = "-";

                        opDto.EndTime = "-";

                    }

                    serial.Operations.Add(opDto);

                }

                job.Serials.Add(serial);

            }

            // ===================== JOB TOTAL HOURS =====================

            job.TotalHours = job.Serials.Sum(s => s.TotalHours ?? 0);

        }

        return Ok(jobs);

    }


    [HttpGet]
    public IActionResult GetNotifications()
    {
        var notifications = _context.Notification
            .OrderByDescending(n => n.CreatedDate)
            .Select(n => new
            {
                n.NotificationID,
                n.Name,
                n.Email,
                n.Subject,
                n.Details,
                n.Status,
                n.ResponseSubject,
                n.ResponseBody,
                n.ResponseStatus,
                n.CreatedDate,
                n.UpdatedDate
            })
            .ToList();

        return Ok(notifications);
    }



    [HttpGet]
    public async Task<IActionResult> GetHoldSubmittedJobs()
    {
        var data = await _context.JobReopenTrack
            .Where(r => r.HoldStatus == "HOLD")
            .OrderByDescending(r => r.SubmittedOn)
            .Select(r => new {
                r.Id,
                holdJobNumber       = r.HoldJobNumber,
                holdSerialNo        = r.HoldSerialNo,
                holdOperationNumber = r.HoldOperationNumber,
                holdWc              = r.HoldWc,
                holdItem            = r.HoldItem,
                holdStatus          = r.HoldStatus,
                prevJobNumber       = r.PrevJobNumber,
                prevSerialNo        = r.PrevSerialNo,
                prevOperationNumber = r.PrevOperationNumber,
                prevWc              = r.PrevWc,
                prevItem            = r.PrevItem,
                prevStatus          = r.PrevStatus,
                newTransNum         = r.NewTransNum,
                submittedBy         = r.SubmittedBy,
                submittedOn         = r.SubmittedOn
            })
            .ToListAsync();
    
        return Ok(new { data });
    }
    
    
    // GET api/Get/GetRejectSubmittedJobs
    [HttpGet]
    public async Task<IActionResult> GetRejectSubmittedJobs()
    {
        var data = await _context.JobReopenTrack
            .Where(r => r.HoldStatus == "REJECTED")
            .OrderByDescending(r => r.SubmittedOn)
            .Select(r => new {
                r.Id,
                holdJobNumber       = r.HoldJobNumber,
                holdSerialNo        = r.HoldSerialNo,
                holdOperationNumber = r.HoldOperationNumber,
                holdWc              = r.HoldWc,
                holdItem            = r.HoldItem,
                holdStatus          = r.HoldStatus,
                prevJobNumber       = r.PrevJobNumber,
                prevSerialNo        = r.PrevSerialNo,
                prevOperationNumber = r.PrevOperationNumber,
                prevWc              = r.PrevWc,
                prevItem            = r.PrevItem,
                prevStatus          = r.PrevStatus,
                newTransNum         = r.NewTransNum,
                submittedBy         = r.SubmittedBy,
                submittedOn         = r.SubmittedOn
            })
            .ToListAsync();
    
        return Ok(new { data });
    }


    [HttpGet]
    public async Task<IActionResult> GetAllCompletedJobs()
    {
        try
        {
            // Get all completed rows (status = "3"), not scrapped
            var completedJobs = await _context.JobTranMst
                .Where(j =>
                    j.status == "3" &&
                    (j.qty_scrapped == null || j.qty_scrapped == 0))
                .ToListAsync();
    
            // Group by Job / Operation / Serial / WC — take latest per group
            var grouped = completedJobs
                .GroupBy(j => new { j.job, j.oper_num, j.SerialNo, j.wc })
                .Select(g => g.OrderByDescending(x => x.trans_date).FirstOrDefault())
                .OrderByDescending(j => j!.trans_date)
                .ToList();
    
            // Build result with employee name and job master data
            var result = new List<object>();
    
            foreach (var j in grouped)
            {
                if (j == null) continue;
    
                var jobMaster = await _context.JobMst
                    .FirstOrDefaultAsync(jm => jm.job == j.job);
    
                var empName = await _context.EmployeeMst
                    .Where(e => e.emp_num == j.emp_num)
                    .Select(e => e.name)
                    .FirstOrDefaultAsync();
    
                result.Add(new
                {
                    trans_num       = j.trans_num,
                    jobNumber       = j.job,
                    serialNo        = j.SerialNo,
                    operationNumber = j.oper_num,
                    wcCode          = j.wc,
                    empNum          = j.emp_num,
                    emp_name        = empName ?? "",
                    qtyReleased     = jobMaster?.qty_released ?? 0,
                    item            = jobMaster?.item ?? j.item ?? "",
                    transType       = j.trans_type,   // "D" = normal, "M" = QC
                    qcgroup         = j.qcgroup ?? "",
                    total_a_hrs     = j.a_hrs ?? 0,
                    trans_date      = j.trans_date,
                    endTime         = j.end_time
                });
            }
    
            return Ok(new { data = result, totalRecords = result.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }



[HttpGet]
public IActionResult GetLatestForm()
{
    var data = _context.JobFormRevMst
        .OrderByDescending(x => x.UpdatedDate)
        .Select(x => new {
            x.Id,
            x.FormNo_revno,
            x.UpdatedBy,
            x.UpdatedDate
        })
        .FirstOrDefault();

    return Ok(data);
}


   







}



