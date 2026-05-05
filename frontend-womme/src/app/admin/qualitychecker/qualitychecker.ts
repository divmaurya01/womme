import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild, NgZone, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin, Subject } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { Table } from 'primeng/table';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TabView, TabViewModule } from 'primeng/tabview';
import Swal from 'sweetalert2';
import { ActivatedRoute, Router } from '@angular/router';
import { LoaderService } from '../../services/loader.service';
import html2pdf from 'html2pdf.js';

@Component({
  selector: 'qualitychecker',
  templateUrl: './qualitychecker.html',
  styleUrls: ['./qualitychecker.scss'],
  standalone: true,
  imports: [ CommonModule, HeaderComponent, SidenavComponent, TableModule, FormsModule, ButtonModule, TabViewModule ]
})
export class QualityChecker implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('dt') dt!: Table;
  @ViewChild('tabView') tabView!: TabView;

  activeTabIndex: number = 0;
  dtTrigger: Subject<any> = new Subject();

  transactions: any[]  = [];
  totalRecords: number = 0;
  page: number         = 0;
  size: number         = 5000;
  searchTerm: string   = '';

  isLoadingNewJobs:   boolean = false;
  isLoadingOngoing:   boolean = false;
  isLoadingCompleted: boolean = false;

  isSidebarHidden  = window.innerWidth <= 1024;
  selectedJobs: any[]  = [];
  ongoingJobs:  any[]  = [];
  completedJobs: any[] = [];
  jobTimers: { [key: string]: any } = {};
  isExtendedMode: boolean = false;
  qaQcEmployees:  any[]   = [];

  // Tracks jobs started this session to prevent stale API reappearance
  private recentlyStartedKeys = new Set<string>();

  constructor(
    private jobService: JobService,
    private router: Router,
    private route: ActivatedRoute,
    private loader: LoaderService,
    private ngZone: NgZone
  ) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.loadQaQcEmployees();

    this.route.queryParams.subscribe(params => {
      const status = params['status'];
      if      (status === 'running' || status === 'paused' || status === 'critical') { this.activeTabIndex = 1; }
      else if (status === 'extended' || status === 'completed')                      { this.activeTabIndex = 2; }
      else                                                                            { this.activeTabIndex = 0; }
    });

    this.loadJobs();
    this.loadOngoingJobs();
    this.loadCompletedJobs();
  }

  ngAfterViewInit(): void { this.dtTrigger.next(true); }
  ngOnDestroy(): void {
    Object.values(this.jobTimers).forEach(t => clearInterval(t));
    this.dtTrigger.unsubscribe();
  }

  showDecisionButtons(job: any) { job.showDecision = true; }

  @HostListener('window:resize')
  onResize() { this.checkScreenSize(); }
  checkScreenSize() { this.isSidebarHidden = window.innerWidth <= 1024; }
  toggleSidebar()   { this.isSidebarHidden = !this.isSidebarHidden; }

  private localNow(): string {
    const n = new Date();
    return `${n.getFullYear()}-${String(n.getMonth()+1).padStart(2,'0')}-${String(n.getDate()).padStart(2,'0')}T${String(n.getHours()).padStart(2,'0')}:${String(n.getMinutes()).padStart(2,'0')}:${String(n.getSeconds()).padStart(2,'0')}`;
  }

  private userDetails() {
    return JSON.parse(localStorage.getItem('userDetails') || '{}');
  }

  // ── stable composite key — job+serial+oper+wc ─────────────────────────
  // Same fields used in active jobs filter so it always matches
  private jobKey(job: any): string {
    return `${(job.jobNumber || job.job || '').trim()}|${(job.serialNo || '').trim()}|${job.operationNumber ?? job.operNum ?? job.oper_num}|${(job.wcCode || job.wc || '').trim()}`;
  }

  // ── QA/QC Employees ───────────────────────────────────────────────────

  loadQaQcEmployees() {
    this.jobService.GetQaQcEmployees().subscribe({
      next: (res: any) => { this.qaQcEmployees = res ?? []; },
      error: (err) => console.error('Failed to load QA/QC employees', err)
    });
  }

  onSearchChange(value: string) {
    this.searchTerm = value;
    this.loadJobs({ first: 0, rows: this.size });
  }

  // ── Tab 1: New Jobs ───────────────────────────────────────────────────

  loadJobs(pageEvent?: any) {
    this.isLoadingNewJobs = true;
    this.loader.show();

    const ud      = this.userDetails();
    const roleId  = Number(ud.roleID);
    const empCode = (ud.employeeCode || '').trim();
    const page    = pageEvent?.first ? pageEvent.first / pageEvent.rows : this.page;
    const size    = pageEvent?.rows ?? this.size;

    forkJoin({
      active: this.jobService.GetActiveQCJobs(),
      qc:     this.jobService.GetQC(page, size, this.searchTerm),
      next:   this.jobService.getIsNextJobActive()
    })
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: ({ active, qc, next }: any) => {
        const activeJobs = active?.data ?? [];
        const qcJobs     = qc?.data     ?? [];

        // Build nextOp map
        const nextOpMap = new Map<string, number[]>();
        (next?.data ?? []).forEach((x: any) => {
          const key = `${x.job}|${x.serialNo}`;
          if (!nextOpMap.has(key)) nextOpMap.set(key, []);
          nextOpMap.get(key)!.push(Number(x.nextOper));
        });

        // Build active jobs set for O(1) lookup
        // Active jobs API returns: job, SerialNo, oper_num, wc, item
        const activeSet = new Set<string>(
          activeJobs.map((a: any) =>
            `${(a.job ?? '').toLowerCase().trim()}|${(a.SerialNo ?? a.serialNo ?? '').toLowerCase().trim()}|${+a.oper_num}|${(a.wc ?? '').toLowerCase().trim()}`
          )
        );

        // Filter out active jobs — match by job+serial+oper+wc (item excluded, can differ)
        const finalJobs = qcJobs.filter((job: any) => {
          const key = `${(job.job ?? '').toLowerCase().trim()}|${(job.serialNo ?? '').toLowerCase().trim()}|${+job.operNum}|${(job.wcCode ?? '').toLowerCase().trim()}`;

          // ✅ Also exclude recently started jobs (guards against stale active API)
          const stableKey = `${(job.job ?? '').trim()}|${(job.serialNo ?? '').trim()}|${+job.operNum}|${(job.wcCode ?? '').trim()}`;
          if (this.recentlyStartedKeys.has(stableKey)) return false;

          return !activeSet.has(key);
        });

        // Map to view model
        this.transactions = finalJobs.map((x: any) => ({
          // stable key: job+serial+oper+wc — no index so it's consistent across reloads
          uniqueRowId:     `${(x.job ?? '').trim()}|${(x.serialNo ?? '').trim()}|${+x.operNum}|${(x.wcCode ?? '').trim()}`,
          serialNo:        x.serialNo.trim(),
          jobNumber:       x.job.trim(),
          qtyReleased:     x.qtyReleased,
          item:            x.item.trim(),
          jobYear:         x.jobYear,
          operationNumber: Number(x.operNum),
          wcCode:          x.wcCode.trim(),
          wcDescription:   x.wcDescription.trim(),
          empNum:          x.empNum,   // camelCase from GetQC API
          status:          x.status,
          isActive:        x.isActive,
          // ✅ Reopen logic: show if status=4 (hold) with reopen_flag
          reopenFlag:      x.reopen_flag ?? false
        }));

        // Role 5: next-job gate + emp assignment filter
        if (roleId === 5) {
          this.transactions = this.transactions.filter((job: any) => {
            const key        = `${job.jobNumber}|${job.serialNo}`;
            const nextOps    = nextOpMap.get(key) ?? [];
            const isNext     = nextOps.includes(Number(job.operationNumber));
            const empList    = (job.empNum || '').split(',').map((e: string) => e.trim());
            const isAssigned = empList.includes(empCode);
            return isNext && isAssigned;
          });
        }

        this.totalRecords     = this.transactions.length;
        this.isLoadingNewJobs = false;
      },
      error: (err) => {
        this.isLoadingNewJobs = false;
        Swal.fire({ icon: 'error', title: 'Error', text: err?.error?.message || 'Something went wrong.' });
      }
    });
  }

  // ── Start Single QC Job ───────────────────────────────────────────────

  startQCJob(job: any) {
    const emp = this.userDetails().employeeCode || '';

    Swal.fire({
      title: 'Start Job?',
      text: `Job ${job.jobNumber} (Serial ${job.serialNo}) at WC ${job.wcCode}?`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Start'
    }).then(result => {
      if (!result.isConfirmed) return;

      this.loader.show();
      this.jobService.startQCJob({
        JobNumber:       job.jobNumber,
        SerialNo:        job.serialNo,
        OperationNumber: job.operationNumber,
        Wc:              job.wcCode,
        item:            job.item,
        QtyReleased:     job.qtyReleased,
        EmpNum:          emp,
        loginuser:       emp,
        startTime:       this.localNow()
      })
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          // ✅ Track stable key — same format used in filter above
          const stableKey = `${job.jobNumber}|${job.serialNo}|${job.operationNumber}|${job.wcCode}`;
          this.recentlyStartedKeys.add(stableKey);

          // Instant removal from UI
          this.transactions = this.transactions.filter(t => t.uniqueRowId !== job.uniqueRowId);
          this.totalRecords = this.transactions.length;

          Swal.fire({
            icon: 'success', title: 'Started!',
            text: `Job ${job.jobNumber} started.`,
            timer: 2000, showConfirmButton: false
          }).then(() => {
            this.loadJobs();
            this.loadOngoingJobs();
            // Clear tracking after 5s — enough for API to catch up
            setTimeout(() => this.recentlyStartedKeys.delete(stableKey), 5000);
          });
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to start job', 'error')
      });
    });
  }

  // ── Start Group QC Jobs ───────────────────────────────────────────────

  StartGroupQCJobs() {
    if (!this.selectedJobs?.length) {
      Swal.fire('No jobs selected', 'Please select at least one job.', 'warning');
      return;
    }

    const emp = this.userDetails().employeeCode || '';

    Swal.fire({
      title: 'Start Selected Jobs?',
      text: `Start ${this.selectedJobs.length} selected job(s)?`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Start'
    }).then(result => {
      if (!result.isConfirmed) return;

      this.loader.show();
      this.jobService.startGroupQCJobs({
        jobs: this.selectedJobs.map(job => ({
          JobNumber:       job.jobNumber,
          SerialNo:        job.serialNo,
          OperationNumber: job.operationNumber,
          Wc:              job.wcCode,
          Item:            job.item,
          QtyReleased:     job.qtyReleased,
          EmpNum:          emp,
          loginuser:       emp,
          StartTime:       this.localNow()
        }))
      })
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any) => {
          // ✅ Track all started keys
          const startedKeys = this.selectedJobs.map(j =>
            `${j.jobNumber}|${j.serialNo}|${j.operationNumber}|${j.wcCode}`
          );
          startedKeys.forEach(k => this.recentlyStartedKeys.add(k));

          // Instant removal
          const startedIds = new Set(this.selectedJobs.map(j => j.uniqueRowId));
          this.transactions = this.transactions.filter(t => !startedIds.has(t.uniqueRowId));
          this.totalRecords = this.transactions.length;
          this.selectedJobs = [];

          Swal.fire({
            icon: 'success', title: 'Processed',
            text: `${res.startedJobs?.length ?? 0} job(s) started.`,
            timer: 2000, showConfirmButton: false
          }).then(() => {
            this.loadJobs();
            this.loadOngoingJobs();
            setTimeout(() => startedKeys.forEach(k => this.recentlyStartedKeys.delete(k)), 5000);
          });
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to start jobs', 'error')
      });
    });
  }

  // ── Tab 2: Ongoing Jobs ───────────────────────────────────────────────

  loadOngoingJobs() {
    this.isLoadingOngoing = true;
    this.loader.show();

    this.jobService.GetActiveQCJobs()
    .pipe(finalize(() => { this.loader.hide(); this.isLoadingOngoing = false; }))
    .subscribe({
      next: (res: any) => {
        const allJobs = res?.data ?? [];
        const ud      = this.userDetails();
        const roleId  = Number(ud.roleID);
        const empCode = (ud.employeeCode || '').trim();

        Object.values(this.jobTimers).forEach(t => clearInterval(t));
        this.jobTimers = {};

        let filtered = allJobs.filter((x: any) => x.status === '1' || x.status === '2');

        // Role 5: only show jobs assigned to this QC employee
        if (roleId === 5) {
          filtered = filtered.filter((x: any) => {
            const empList = (x.emp_num || '').split(',').map((e: string) => e.trim());
            return empList.includes(empCode);
          });
        }

        this.ongoingJobs = filtered.map((x: any, index: number) => {
          const jobKey    = `${x.job}-${x.serialNo}-${x.oper_num}-${x.wc}-${index}`;
          const startTime = new Date(x.start_time);
          const now       = new Date();

          let elapsedSeconds = x.status === '1'
            ? Math.floor((now.getTime() - startTime.getTime()) / 1000) + Math.floor((x.total_a_hrs || 0) * 3600)
            : Math.floor((x.total_a_hrs || 0) * 3600);
          if (elapsedSeconds < 0) elapsedSeconds = 0;

          const jobObj: any = {
            uniqueRowId:     jobKey,
            jobNumber:       x.job,
            serialNo:        x.serialNo,
            operationNumber: x.oper_num,
            wcCode:          x.wc,
            item:            x.item,
            empNum:          x.emp_num,
            emp_name:        x.emp_name,
            trans_num:       x.trans_num ?? null,
            startTime,
            endTime:         x.end_time ? new Date(x.end_time) : null,
            elapsedSeconds,
            elapsedTime:     this.formatElapsedTime(elapsedSeconds),
            isPaused:        x.status === '2',
            remark:          x.ongoing_comment ?? '',
            qcGroup:         x.qcgroup ?? null,
            timerInterval:   null
          };

          if (x.status === '1') {
            jobObj.timerInterval = setInterval(() => {
              jobObj.elapsedSeconds++;
              jobObj.elapsedTime = this.formatElapsedTime(jobObj.elapsedSeconds);
            }, 1000);
            this.jobTimers[jobKey] = jobObj.timerInterval;
          }

          return jobObj;
        });
      },
      error: (err) => console.error('Failed to load ongoing jobs', err)
    });
  }

  formatElapsedTime(s: number): string {
    if (s < 0) s = 0;
    const h   = Math.floor(s / 3600);
    const m   = Math.floor((s % 3600) / 60);
    const sec = Math.floor(s % 60);
    return `${String(h).padStart(2,'0')}:${String(m).padStart(2,'0')}:${String(sec).padStart(2,'0')}`;
  }

  formatTime(hours: number): string {
    if (!hours || hours <= 0) return '00:00:00';
    return this.formatElapsedTime(Math.floor(hours * 3600));
  }

  // ── Pause / Resume ────────────────────────────────────────────────────

  togglePauseResume(job: any) {
    const emp     = this.userDetails().employeeCode || '';
    const payload = {
      JobNumber: job.jobNumber, SerialNo: job.serialNo,
      OperationNumber: job.operationNumber, Wc: job.wcCode,
      Item: job.item, EmpNum: emp, loginuser: emp, startTime: this.localNow()
    };

    this.loader.show();

    if (job.isPaused) {
      this.jobService.startQCJob(payload)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          job.isPaused      = false;
          job.timerInterval = this.ngZone.run(() =>
            setInterval(() => { job.elapsedSeconds++; job.elapsedTime = this.formatElapsedTime(job.elapsedSeconds); }, 1000)
          );
          this.jobTimers[job.uniqueRowId] = job.timerInterval;
          Swal.fire({ icon: 'success', title: 'Resumed', text: `Job ${job.jobNumber} resumed.`, timer: 2000, showConfirmButton: false });
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to resume', 'error')
      });
    } else {
      this.jobService.pauseSingleQCJob(payload)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          job.isPaused = true;
          if (job.timerInterval) { clearInterval(job.timerInterval); delete this.jobTimers[job.uniqueRowId]; }
          Swal.fire({ icon: 'success', title: 'Paused', text: `Job ${job.jobNumber} paused.`, timer: 2000, showConfirmButton: false });
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to pause', 'error')
      });
    }
  }

  // ── Accept / Hold / Reject ────────────────────────────────────────────

  completeQCJobWithEmployee(job: any, empNum: string) {
    this.loader.show();
    this.jobService.completeSingleQCJob({
      JobNumber: job.jobNumber, SerialNo: job.serialNo,
      OperationNumber: job.operationNumber, Wc: job.wcCode,
      Item: job.item, EmpNum: empNum, loginuser: empNum, startTime: this.localNow()
    })
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: () => {
        Swal.fire({ icon: 'success', title: 'Completed!', text: `Job ${job.jobNumber} accepted.`, timer: 2000, showConfirmButton: false })
          .then(() => { this.loadJobs(); this.loadOngoingJobs(); this.loadCompletedJobs(); this.activeTabIndex = 2; });
      },
      error: (err) => Swal.fire('Error', err.error?.message || 'Failed to complete', 'error')
    });
  }

  acceptJob(job: any) {
    const opts = this.qaQcEmployees.map(e => `<option value="${e.empNum}">${e.name} (${e.empNum})</option>`).join('');
    Swal.fire({
      title: 'Inspected By',
      html: `<div style="text-align:left;">
               <label style="font-size:13px;font-weight:600;margin-bottom:8px;display:block;">Select Approving QA/QC Employee</label>
               <select id="swal-emp-select" style="width:100%;padding:10px;border:1.5px solid #ddd;border-radius:8px;font-size:14px;">
                 <option value="">-- Select Employee --</option>${opts}
               </select>
             </div>`,
      showCancelButton: true, confirmButtonText: 'Accept & Complete',
      confirmButtonColor: '#2e7d32', cancelButtonColor: '#6c757d',
      preConfirm: () => {
        const sel = (document.getElementById('swal-emp-select') as HTMLSelectElement)?.value;
        if (!sel) { Swal.showValidationMessage('Please select an employee'); return false; }
        return sel;
      }
    }).then(result => { if (result.isConfirmed) this.completeQCJobWithEmployee(job, result.value); });
  }

  private buildJobPayload(job: any, remark?: string) {
    const emp = this.userDetails().employeeCode || '';
    return { JobNumber: job.jobNumber, SerialNo: job.serialNo, OperationNumber: job.operationNumber, Wc: job.wcCode, Item: job.item, EmpNum: emp, loginuser: emp, startTime: this.localNow(), remark };
  }

  holdJob(job: any) {
    Swal.fire({
      title: 'Hold Job', input: 'textarea', inputLabel: 'Enter Hold Comment',
      inputPlaceholder: 'Comment is required', showCancelButton: true, confirmButtonText: 'Submit',
      preConfirm: (v) => { if (!v?.trim()) { Swal.showValidationMessage('Comment is required'); } return v; }
    }).then(result => {
      if (!result.isConfirmed) return;
      this.loader.show();
      this.jobService.holdQCJob(this.buildJobPayload(job, result.value))
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          Swal.fire({ icon: 'info', title: 'On Hold', text: 'Job moved to Hold.', timer: 2000, showConfirmButton: false })
            .then(() => { this.loadJobs(); this.loadOngoingJobs(); this.loadCompletedJobs(); });
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to hold job', 'error')
      });
    });
  }

  rejectJob(job: any) {
    Swal.fire({
      title: 'Reject Job', input: 'textarea', inputLabel: 'Enter Reject Comment',
      inputPlaceholder: 'Comment is required', showCancelButton: true, confirmButtonText: 'Submit',
      preConfirm: (v) => { if (!v?.trim()) { Swal.showValidationMessage('Comment is required'); } return v; }
    }).then(result => {
      if (!result.isConfirmed) return;
      this.loader.show();
      this.jobService.rejectQCJob(this.buildJobPayload(job, result.value))
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          Swal.fire({ icon: 'warning', title: 'Rejected', text: 'Job has been rejected.', timer: 2000, showConfirmButton: false })
            .then(() => { this.loadJobs(); this.loadOngoingJobs(); this.loadCompletedJobs(); });
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to reject job', 'error')
      });
    });
  }

  // ── Tab 3: Completed Jobs ─────────────────────────────────────────────

  loadCompletedJobs() {
    this.isLoadingCompleted = true;
    this.loader.show();

    this.jobService.GetCompletedQCJobs()
    .pipe(finalize(() => { this.loader.hide(); this.isLoadingCompleted = false; }))
    .subscribe({
      next: (res: any) => {
        let jobs = res.data ?? [];
        const ud  = this.userDetails();
        const emp = (ud.employeeCode || '').trim();
        const rid = Number(ud.roleID);

        // Role 5: filter by emp — completed jobs empNum is single value
        if (rid !== 1 && rid !== 2) {
          jobs = jobs.filter((job: any) => {
            // ✅ Handle comma-separated empNum for completed jobs too
            const empList = (job.empNum || '').split(',').map((e: string) => e.trim());
            return empList.includes(emp);
          });
        }

        const grouped: { [k: string]: any[] } = jobs.reduce((acc: any, row: any) => {
          const key = `${row.jobNumber}|${row.operationNumber}|${row.serialNo}|${row.qcgroup}`;
          if (!acc[key]) acc[key] = [];
          acc[key].push(row);
          return acc;
        }, {});

        this.completedJobs = Object.values(grouped).map((group: any[]) => {
          group.sort((a, b) => new Date(b.endTime || 0).getTime() - new Date(a.endTime || 0).getTime());
          const r = group[0];
          return { ...r, compositeKey: `${r.jobNumber}-${r.serialNo}-${r.operationNumber}-${r.qcgroup}`, allLogs: group };
        });
      },
      error: (err) => console.error('Failed to load completed jobs', err)
    });
  }

  // ── Remarks ───────────────────────────────────────────────────────────

  saveQCRemark(job: any) {
    if (!job.remark?.trim()) return;
    this.jobService.updateQCRemark({ trans_num: job.trans_num, column: 'ongoing_comment', remark: job.remark }).subscribe({
      next: (res: any) => Swal.fire({ icon: 'success', title: 'Remark Updated', text: res.message, timer: 2000, showConfirmButton: false }),
      error: (err: any) => Swal.fire('Error', err.error?.message || 'Failed to update remark', 'error')
    });
  }

  savecompleteRemark(job: any) {
    if (!job.remark?.trim()) return;
    this.jobService.updateQCRemark({ trans_num: job.trans_num, column: 'completed_comment', remark: job.remark }).subscribe({
      next: (res: any) => Swal.fire({ icon: 'success', title: 'Remark Updated', text: res.message, timer: 2000, showConfirmButton: false }),
      error: (err: any) => Swal.fire('Error', err.error?.message || 'Failed to update remark', 'error')
    });
  }

  // ── Report ────────────────────────────────────────────────────────────

  viewReport(job: any) {
    if (!job) return;
    this.loader.show();
    this.jobService.GetJobReport(job.jobNumber)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (data: any) => {
        if (!data) return;
        const wrapper = document.createElement('div');
        let html = `<h2>Job Report: ${data.job}</h2>
                    <p>Date: ${data.jobDate ? new Date(data.jobDate).toLocaleDateString() : '---'}</p>
                    <p>Item: ${data.item || '---'}</p>`;
        (data.operations || []).forEach((op: any) => {
          html += `<h3>Operation: ${op.operNum} - ${op.operationDescription || ''}</h3>`;
        });
        wrapper.innerHTML = html;
        document.body.appendChild(wrapper);
        html2pdf()
          .set({ filename: `${data.job}_Report.pdf`, html2canvas: { scale: 2 }, jsPDF: { unit: 'pt', format: 'a4', orientation: 'portrait' } })
          .from(wrapper).save()
          .then(() => document.body.removeChild(wrapper));
      },
      error: (err) => console.error('Failed to fetch job report', err)
    });
  }

  // ── Extended job check ────────────────────────────────────────────────

  isExtendedJob(job: any): boolean {
    if (job.total_a_hrs && job.total_a_hrs > 8) return true;
    if (job.allLogs && job.allLogs.length > 2)  return true;
    return false;
  }
}