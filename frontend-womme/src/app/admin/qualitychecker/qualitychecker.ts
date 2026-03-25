import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild, NgZone, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject } from 'rxjs';
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
import { finalize } from 'rxjs/operators';
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
  isLoading: boolean   = false;
  isSidebarHidden      = window.innerWidth <= 1024;
  selectedJobs: any[]  = [];
  ongoingJobs: any[]   = [];
  completedJobs: any[] = [];
  jobTimers: { [key: string]: any } = {};
  isExtendedMode: boolean = false;
  qaQcEmployees: any[] = [];

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
  showDecisionButtons(job: any) { job.showDecision = true; }

  ngOnDestroy(): void {
    Object.values(this.jobTimers).forEach(timer => clearInterval(timer));
    this.dtTrigger.unsubscribe();
  }

  @HostListener('window:resize')
  onResize() { this.checkScreenSize(); }
  checkScreenSize() { this.isSidebarHidden = window.innerWidth <= 1024; }
  toggleSidebar()   { this.isSidebarHidden = !this.isSidebarHidden; }

  private localNow(): string {
    const now = new Date();
    return now.getFullYear() + '-' + String(now.getMonth()+1).padStart(2,'0') + '-' + String(now.getDate()).padStart(2,'0') + 'T' +
           String(now.getHours()).padStart(2,'0') + ':' + String(now.getMinutes()).padStart(2,'0') + ':' + String(now.getSeconds()).padStart(2,'0');
  }

  loadQaQcEmployees() {
    this.jobService.GetQaQcEmployees().subscribe({
      next: (res: any) => { this.qaQcEmployees = res ?? []; },
      error: (err) => console.error('Failed to load QA/QC employees', err)
    });
  }

  onSearchChange(value: string) { this.searchTerm = value; this.loadJobs({ first: 0, rows: this.size }); }

  // ── Tab 1: New Jobs ───────────────────────────────────────────────────

  loadJobs(pageEvent?: any) {
    this.isLoading = true;
    const page   = pageEvent?.first ? pageEvent.first / pageEvent.rows : this.page;
    const size   = pageEvent?.rows ?? this.size;
    const ud     = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const roleid = Number(ud.roleID);

    this.loader.show();

    this.jobService.GetActiveQCJobs().subscribe({
      next: (activeRes: any) => {
        const activeJobs = activeRes?.data ?? [];

        this.jobService.GetQC(page, size, this.searchTerm)
          .pipe(finalize(() => this.loader.hide()))
          .subscribe({
            next: (qcRes: any) => {
              const qcJobs = qcRes?.data ?? [];

              this.jobService.getIsNextJobActive().subscribe({
                next: (nextRes: any) => {
                  const nextOpMap = new Map<string, number[]>();
                  (nextRes.data ?? []).forEach((x: any) => {
                    const key = `${x.job}|${x.serialNo}`;
                    if (!nextOpMap.has(key)) nextOpMap.set(key, []);
                    nextOpMap.get(key)!.push(Number(x.nextOper));
                  });

                  let finalJobs = qcJobs.filter((job: any) => {

                  const jobSerial = (job.serialNo ?? '').toLowerCase().trim();
                  const jobOper   = +job.operNum;
                  const jobWc     = (job.wcCode ?? '').toLowerCase().trim();
                  const jobItem   = (job.item ?? '').toLowerCase().trim();
                  const jobJob    = (job.job ?? '').toLowerCase().trim();

                  return !activeJobs.some((active: any) => {

                    const aSerial = (active.SerialNo ?? active.serialNo ?? '').toLowerCase().trim();
                    const aOper   = +active.oper_num;
                    const aWc     = (active.wc ?? '').toLowerCase().trim();
                    const aItem   = (active.item ?? '').toLowerCase().trim();
                    const aJob    = (active.job ?? '').toLowerCase().trim();

                    return (
                      aJob === jobJob &&
                      aSerial === jobSerial &&
                      aOper === jobOper &&
                      aWc === jobWc &&
                      aItem === jobItem
                    );
                  });
                });

                  this.transactions = finalJobs.map((x: any, index: number) => ({
                    uniqueRowId:     `${x.serialNo}-${x.operNum}-${x.wcCode}-${index}`,
                    serialNo:        x.serialNo.trim(),
                    jobNumber:       x.job.trim(),
                    qtyReleased:     x.qtyReleased,
                    item:            x.item.trim(),
                    jobYear:         x.jobYear,
                    operationNumber: Number(x.operNum),
                    wcCode:          x.wcCode.trim(),
                    wcDescription:   x.wcDescription.trim(),
                    empNum:          x.emp_num,
                    status:          x.status,
                    isActive:        x.isActive
                  }));

                  this.totalRecords = this.transactions.length;
                  this.isLoading    = false;
                  this.loader.hide();
                },
                error: err => this.handleError(err)
              });
            },
            error: err => this.handleError(err)
          });
      },
      error: err => this.handleError(err)
    });
  }

  private handleError(err: any) {
    this.isLoading = false;
    this.loader.hide();
    Swal.fire({ icon: 'error', title: 'Error', text: err?.error?.message || 'Something went wrong.' });
  }

  isExtendedJob(job: any): boolean {
    if (job.total_a_hrs && job.total_a_hrs > 8) return true;
    if (job.allLogs && job.allLogs.length > 2)  return true;
    return false;
  }

  startQCJob(job: any) {
    const ud = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const emp = ud.employeeCode || '';
    Swal.fire({ title: 'Start Job?', text: `Job ${job.jobNumber} (Serial ${job.serialNo}) at WC ${job.wcCode}?`, icon: 'question', showCancelButton: true, confirmButtonText: 'Yes, Start' })
      .then(result => {
        if (result.isConfirmed) {
          this.loader.show();
          this.jobService.startQCJob({ JobNumber: job.jobNumber, SerialNo: job.serialNo, OperationNumber: job.operationNumber, Wc: job.wcCode, item: job.item, QtyReleased: job.qtyReleased, EmpNum: emp, loginuser: emp, startTime: this.localNow() })
            .pipe(finalize(() => this.loader.hide())).subscribe({
              next:  () => { Swal.fire('Started!', `Job ${job.jobNumber} started.`, 'success'); this.loadJobs(); this.loadOngoingJobs(); },
              error: (err) => Swal.fire('Error', err.error?.message || 'Failed to start job', 'error')
            });
        }
      });
  }

  StartGroupQCJobs() {
    const ud = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const emp = ud.employeeCode || '';
    if (!this.selectedJobs?.length) { Swal.fire('No jobs selected', 'Please select at least one job.', 'warning'); return; }
    Swal.fire({ title: 'Start Selected Jobs?', text: `Start ${this.selectedJobs.length} selected jobs?`, icon: 'question', showCancelButton: true, confirmButtonText: 'Yes, Start' })
      .then(result => {
        if (result.isConfirmed) {
          this.loader.show();
          this.jobService.startGroupQCJobs({ jobs: this.selectedJobs.map(job => ({ JobNumber: job.jobNumber, SerialNo: job.serialNo, OperationNumber: job.operationNumber, Wc: job.wcCode, Item: job.item, QtyReleased: job.qtyReleased, EmpNum: emp, loginuser: emp, StartTime: this.localNow() })) })
            .pipe(finalize(() => this.loader.hide())).subscribe({
              next: (res: any) => { Swal.fire('Processed', `${res.startedJobs?.length ?? 0} started.`, 'success'); this.loadJobs(); this.loadOngoingJobs(); this.selectedJobs = []; },
              error: (err) => Swal.fire('Error', err.error?.message || 'Failed', 'error')
            });
        }
      });
  }

  // ── Tab 2: Ongoing Jobs ───────────────────────────────────────────────

  loadOngoingJobs() {
    this.isLoading = true;
    this.loader.show();

    this.jobService.GetActiveQCJobs().subscribe({
      next: (res: any) => {
        const allJobs = res?.data ?? [];
        Object.values(this.jobTimers).forEach(t => clearInterval(t));
        this.jobTimers = {};

        this.ongoingJobs = allJobs
          .filter((x: any) => x.status === '1' || x.status === '2')
          .map((x: any, index: number) => {
            const jobKey    = `${x.job}-${x.serialNo}-${x.oper_num}-${x.wc}-${index}`;
            const startTime = new Date(x.start_time);
            const now       = new Date();

            let elapsedSeconds = x.status === '1'
              ? Math.floor((now.getTime() - startTime.getTime()) / 1000) + Math.floor((x.total_a_hrs || 0) * 3600)
              : Math.floor((x.total_a_hrs || 0) * 3600);
            if (elapsedSeconds < 0) elapsedSeconds = 0;

            const jobObj: any = {
              uniqueRowId: jobKey, jobNumber: x.job, serialNo: x.serialNo,
              operationNumber: x.oper_num, wcCode: x.wc, item: x.item,
              empNum: x.emp_num, emp_name: x.emp_name, trans_num: x.trans_num ?? null,
              startTime, endTime: x.end_time ? new Date(x.end_time) : null,
              elapsedSeconds, elapsedTime: this.formatElapsedTime(elapsedSeconds),
              isPaused: x.status === '2', remark: x.ongoing_comment ?? '',
              qcGroup: x.qcgroup ?? null, timerInterval: null
            };

            if (x.status === '1') {
              jobObj.timerInterval = setInterval(() => { jobObj.elapsedSeconds++; jobObj.elapsedTime = this.formatElapsedTime(jobObj.elapsedSeconds); }, 1000);
              this.jobTimers[jobKey] = jobObj.timerInterval;
            }

            return jobObj;
          });

        this.isLoading = false;
        this.loader.hide();
      },
      error: err => { console.error(err); this.isLoading = false; this.loader.hide(); }
    });
  }

  formatElapsedTime(s: number): string {
    if (s < 0) s = 0;
    return `${Math.floor(s/3600).toString().padStart(2,'0')}:${Math.floor((s%3600)/60).toString().padStart(2,'0')}:${Math.floor(s%60).toString().padStart(2,'0')}`;
  }

  formatTime(hours: number): string {
    if (!hours || hours <= 0) return '00:00:00';
    const s = Math.floor(hours * 3600);
    return `${Math.floor(s/3600).toString().padStart(2,'0')}:${Math.floor((s%3600)/60).toString().padStart(2,'0')}:${Math.floor(s%60).toString().padStart(2,'0')}`;
  }

  togglePauseResume(job: any) {
    const ud = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const emp = ud.employeeCode || '';
    const payload = { JobNumber: job.jobNumber, SerialNo: job.serialNo, OperationNumber: job.operationNumber, Wc: job.wcCode, Item: job.item, EmpNum: emp, loginuser: emp, startTime: this.localNow() };

    this.loader.show();
    if (job.isPaused) {
      this.jobService.startQCJob(payload).pipe(finalize(() => this.loader.hide())).subscribe({
        next: () => {
          job.isPaused = false;
          job.timerInterval = this.ngZone.run(() => setInterval(() => { job.elapsedSeconds++; job.elapsedTime = this.formatElapsedTime(job.elapsedSeconds); }, 1000));
          this.jobTimers[job.uniqueRowId] = job.timerInterval;
          Swal.fire('Resumed', `Job ${job.jobNumber} resumed.`, 'success');
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to resume', 'error')
      });
    } else {
      this.jobService.pauseSingleQCJob(payload).pipe(finalize(() => this.loader.hide())).subscribe({
        next: () => {
          job.isPaused = true;
          if (job.timerInterval) { clearInterval(job.timerInterval); delete this.jobTimers[job.uniqueRowId]; }
          Swal.fire('Paused', `Job ${job.jobNumber} paused.`, 'success');
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to pause', 'error')
      });
    }
  }

  completeQCJobWithEmployee(job: any, empNum: string) {
    this.loader.show();
    this.jobService.completeSingleQCJob({ JobNumber: job.jobNumber, SerialNo: job.serialNo, OperationNumber: job.operationNumber, Wc: job.wcCode, Item: job.item, EmpNum: empNum, loginuser: empNum, startTime: this.localNow() })
      .pipe(finalize(() => this.loader.hide())).subscribe({
        next: () => { Swal.fire('Completed!', `Job ${job.jobNumber} accepted and completed.`, 'success'); this.loadJobs(); this.loadOngoingJobs(); this.loadCompletedJobs(); this.activeTabIndex = 2; },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to complete', 'error')
      });
  }

  acceptJob(job: any) {
    const opts = this.qaQcEmployees.map(e => `<option value="${e.empNum}">${e.name} (${e.empNum})</option>`).join('');
    Swal.fire({
      title: 'Inspected By',
      html: `<div style="text-align:left;"><label style="font-size:13px;font-weight:600;margin-bottom:8px;display:block;">Select Approving QA/QC Employee</label><select id="swal-emp-select" style="width:100%;padding:10px;border:1.5px solid #ddd;border-radius:8px;font-size:14px;"><option value="">-- Select Employee --</option>${opts}</select></div>`,
      showCancelButton: true, confirmButtonText: 'Accept & Complete', confirmButtonColor: '#2e7d32', cancelButtonColor: '#6c757d',
      preConfirm: () => { const sel = (document.getElementById('swal-emp-select') as HTMLSelectElement)?.value; if (!sel) { Swal.showValidationMessage('Please select an employee'); return false; } return sel; }
    }).then(result => { if (result.isConfirmed) this.completeQCJobWithEmployee(job, result.value); });
  }

  buildJobPayload(job: any, remark?: string) {
    const ud = JSON.parse(localStorage.getItem('userDetails') || '{}');
    return { JobNumber: job.jobNumber, SerialNo: job.serialNo, OperationNumber: job.operationNumber, Wc: job.wcCode, Item: job.item, EmpNum: ud.employeeCode || '', loginuser: ud.employeeCode || '', startTime: this.localNow(), remark };
  }

  holdJob(job: any) {
    Swal.fire({ title: 'Hold Job', input: 'textarea', inputLabel: 'Enter Hold Comment', inputPlaceholder: 'Comment is required', showCancelButton: true, confirmButtonText: 'Submit', preConfirm: (v) => { if (!v?.trim()) Swal.showValidationMessage('Comment is required'); return v; } })
      .then(result => {
        if (result.isConfirmed) {
          this.loader.show();
          this.jobService.holdQCJob(this.buildJobPayload(job, result.value)).pipe(finalize(() => this.loader.hide())).subscribe({
            next: () => { Swal.fire('On Hold', 'Job moved to Hold', 'info'); this.loadJobs(); this.loadOngoingJobs(); this.loadCompletedJobs(); },
            error: (err) => Swal.fire('Error', err.error?.message || 'Failed to hold job', 'error')
          });
        }
      });
  }

  rejectJob(job: any) {
    Swal.fire({ title: 'Reject Job', input: 'textarea', inputLabel: 'Enter Reject Comment', inputPlaceholder: 'Comment is required', showCancelButton: true, confirmButtonText: 'Submit', preConfirm: (v) => { if (!v?.trim()) Swal.showValidationMessage('Comment is required'); return v; } })
      .then(result => {
        if (result.isConfirmed) {
          this.loader.show();
          this.jobService.rejectQCJob(this.buildJobPayload(job, result.value)).pipe(finalize(() => this.loader.hide())).subscribe({
            next: () => { Swal.fire('Rejected', 'Job Rejected', 'warning'); this.loadJobs(); this.loadOngoingJobs(); this.loadCompletedJobs(); },
            error: (err) => Swal.fire('Error', err.error?.message || 'Failed to reject job', 'error')
          });
        }
      });
  }

  // ── Tab 3: Completed Jobs ─────────────────────────────────────────────

  loadCompletedJobs() {
    this.loader.show();
    this.jobService.GetCompletedQCJobs().pipe(finalize(() => this.loader.hide())).subscribe({
      next: (res: any) => {
        let jobs = res.data ?? [];
        const ud  = JSON.parse(localStorage.getItem('userDetails') || '{}');
        const emp = (ud.employeeCode || '').trim();
        const rid = Number(ud.roleID);
        if (rid !== 1) jobs = jobs.filter((job: any) => (job.empNum || '').trim() === emp);

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
        this.isLoading = false;
      },
      error: (err) => { console.error(err); this.isLoading = false; }
    });
  }

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

  viewReport(job: any) {
    if (!job) return;
    this.loader.show();
    this.jobService.GetJobReport(job.jobNumber).pipe(finalize(() => this.loader.hide())).subscribe({
      next: (data: any) => {
        if (!data) return;
        const wrapper = document.createElement('div');
        let html = `<h2>Job Report: ${data.job}</h2><p>Date: ${data.jobDate ? new Date(data.jobDate).toLocaleDateString() : '---'}</p><p>Item: ${data.item || '---'}</p>`;
        (data.operations || []).forEach((op: any) => { html += `<h3>Operation: ${op.operNum} - ${op.operationDescription || ''}</h3>`; });
        wrapper.innerHTML = html;
        document.body.appendChild(wrapper);
        html2pdf().set({ filename: `${data.job}_Report.pdf`, html2canvas: { scale: 2 }, jsPDF: { unit: 'pt', format: 'a4', orientation: 'portrait' } }).from(wrapper).save().then(() => document.body.removeChild(wrapper));
      },
      error: (err) => console.error('Failed to fetch job report', err)
    });
  }
}