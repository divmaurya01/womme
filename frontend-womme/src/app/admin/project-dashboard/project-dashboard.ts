import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild, HostListener } from '@angular/core';
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
import { ActivatedRoute, Router } from '@angular/router';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
import Swal from 'sweetalert2';

@Component({
  selector: 'project-dashboard',
  templateUrl: './project-dashboard.html',
  styleUrls: ['./project-dashboard.scss'],
  standalone: true,
  imports: [
    CommonModule,
    HeaderComponent,
    SidenavComponent,
    TableModule,
    FormsModule,
    ButtonModule,
    TabViewModule
  ]
})
export class ProjectDashboardComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('dt') dt!: Table;
  @ViewChild('tabView') tabView!: TabView;

  activeTabIndex: number = 0;
  dtTrigger: Subject<any> = new Subject();

  isSidebarHidden = window.innerWidth <= 1024;
  isLoading: boolean = false;

  // ── Tab data ──────────────────────────────────────────────────
  holdJobs: any[]            = [];
  rejectedJobs: any[]        = [];
  holdSubmittedJobs: any[]   = [];
  rejectSubmittedJobs: any[] = [];

  // ── Reopen Dialog state ───────────────────────────────────────
  showReopenDialog: boolean       = false;
  selectedHoldJob: any            = null;
  reopenSource: 'hold' | 'reject' = 'hold';
  previousCompletedJobs: any[]    = [];
  selectedPrevJob: any            = null;
  loadingPrevJobs: boolean        = false;
  submittingReopen: boolean       = false;

  // ── Dialog meta ───────────────────────────────────────────────
  currentUser: string = '';
  nowLabel: string    = '';

  constructor(
    private jobService: JobService,
    private router: Router,
    private route: ActivatedRoute,
    private loader: LoaderService
  ) {}

  // ─────────────────────────────────────────────────────────────
  // Lifecycle
  // ─────────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.checkScreenSize();

    const ud = JSON.parse(localStorage.getItem('userDetails') || '{}');
    this.currentUser = ud.employeeCode || 'Unknown';

    this.route.queryParams.subscribe(params => {
      const status = params['status'];
      if      (status === 'reject')        { this.activeTabIndex = 1; }
      else if (status === 'holdSubmit')    { this.activeTabIndex = 2; }
      else if (status === 'rejectSubmit')  { this.activeTabIndex = 3; }
      else                                 { this.activeTabIndex = 0; }
    });

    this.loadHoldJobs();
    this.loadRejectedJobs();
    this.loadHoldSubmittedJobs();
    this.loadRejectSubmittedJobs();
  }

  ngAfterViewInit(): void { this.dtTrigger.next(true); }
  ngOnDestroy(): void     { this.dtTrigger.unsubscribe(); }

  // ─────────────────────────────────────────────────────────────
  // Layout
  // ─────────────────────────────────────────────────────────────

  @HostListener('window:resize')
  onResize() { this.checkScreenSize(); }

  checkScreenSize() { this.isSidebarHidden = window.innerWidth <= 1024; }
  toggleSidebar()   { this.isSidebarHidden = !this.isSidebarHidden; }

  // ─────────────────────────────────────────────────────────────
  // Helpers
  // ─────────────────────────────────────────────────────────────

  formatTime(hours: number): string {
    if (!hours || hours <= 0) return '00:00:00';
    const s = Math.floor(hours * 3600);
    const h = Math.floor(s / 3600).toString().padStart(2, '0');
    const m = Math.floor((s % 3600) / 60).toString().padStart(2, '0');
    const sec = Math.floor(s % 60).toString().padStart(2, '0');
    return `${h}:${m}:${sec}`;
  }

  private localNow(): string {
    const now = new Date();
    return (
      now.getFullYear() + '-' +
      String(now.getMonth() + 1).padStart(2, '0') + '-' +
      String(now.getDate()).padStart(2, '0') + 'T' +
      String(now.getHours()).padStart(2, '0') + ':' +
      String(now.getMinutes()).padStart(2, '0') + ':' +
      String(now.getSeconds()).padStart(2, '0')
    );
  }

  private groupJobs(jobs: any[]): any[] {
    const grouped: { [key: string]: any[] } = jobs.reduce((acc: any, row: any) => {
      const key = `${row.jobNumber}|${row.operationNumber}|${row.serialNo}|${row.qcgroup}`;
      if (!acc[key]) acc[key] = [];
      acc[key].push(row);
      return acc;
    }, {});

    return Object.values(grouped).map((group: any[]) => {
      group.sort((a, b) =>
        new Date(b.endTime || 0).getTime() - new Date(a.endTime || 0).getTime()
      );
      const r = group[0];
      return {
        ...r,
        compositeKey: `${r.jobNumber}-${r.serialNo}-${r.operationNumber}-${r.qcgroup}`,
        allLogs: group
      };
    });
  }

  // Group for ALL completed jobs (uses operationNumber + wcCode as key since no qcgroup on normal jobs)
  private groupAllCompleted(jobs: any[]): any[] {
    const grouped: { [key: string]: any[] } = jobs.reduce((acc: any, row: any) => {
      const key = `${row.jobNumber}|${row.serialNo}|${row.operationNumber}|${row.wcCode}|${row.transType}`;
      if (!acc[key]) acc[key] = [];
      acc[key].push(row);
      return acc;
    }, {});

    return Object.values(grouped).map((group: any[]) => {
      group.sort((a, b) =>
        new Date(b.endTime || b.trans_date || 0).getTime() -
        new Date(a.endTime || a.trans_date || 0).getTime()
      );
      const r = group[0];
      return {
        ...r,
        compositeKey: `${r.jobNumber}-${r.serialNo}-${r.operationNumber}-${r.wcCode}-${r.transType}`
      };
    });
  }

  private applyRoleFilter(jobs: any[]): any[] {
    const ud = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const emp = (ud.employeeCode || '').trim();
    const rid = Number(ud.roleID);
    if (rid !== 1 && rid !== 2) {
      return jobs.filter((j: any) => (j.empNum || '').trim() === emp);
    }
    return jobs;
  }

  // ─────────────────────────────────────────────────────────────
  // Load tab data
  // ─────────────────────────────────────────────────────────────

  loadHoldJobs() {
    this.isLoading = true;
    this.loader.show();
    this.jobService.GetHoldQCJobs()
      .pipe(finalize(() => { this.isLoading = false; this.loader.hide(); }))
      .subscribe({
        next: (res: any) => {
          this.holdJobs = this.groupJobs(this.applyRoleFilter(res.data ?? []));
        },
        error: (err) => console.error('Error fetching hold jobs:', err)
      });
  }

  loadRejectedJobs() {
    this.isLoading = true;
    this.loader.show();
    this.jobService.GetRejectedQCJobs()
      .pipe(finalize(() => { this.isLoading = false; this.loader.hide(); }))
      .subscribe({
        next: (res: any) => {
          this.rejectedJobs = this.groupJobs(this.applyRoleFilter(res.data ?? []));
        },
        error: (err) => console.error('Error fetching rejected jobs:', err)
      });
  }

  loadHoldSubmittedJobs() {
    this.loader.show();
    this.jobService.GetHoldSubmittedJobs()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any) => { this.holdSubmittedJobs = res.data ?? []; },
        error: (err) => console.error('Error fetching hold submitted jobs:', err)
      });
  }

  loadRejectSubmittedJobs() {
    this.loader.show();
    this.jobService.GetRejectSubmittedJobs()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any) => { this.rejectSubmittedJobs = res.data ?? []; },
        error: (err) => console.error('Error fetching reject submitted jobs:', err)
      });
  }

  // ─────────────────────────────────────────────────────────────
  // Reopen Dialog — Open
  // ─────────────────────────────────────────────────────────────

  openReopenDialog(holdJob: any, source: 'hold' | 'reject' = 'hold') {
    this.selectedHoldJob       = holdJob;
    this.reopenSource          = source;
    this.selectedPrevJob       = null;
    this.previousCompletedJobs = [];
    this.showReopenDialog      = true;
    this.nowLabel              = new Date().toLocaleString();
    this.loadPreviousCompletedJobs(holdJob);
  }

  onOverlayClick(event: MouseEvent) {
    this.closeReopenDialog();
  }

  // ─────────────────────────────────────────────────────────────
  // Load ALL completed jobs for this job + serial (QC + normal)
  // Uses GetAllCompletedJobs — not just QC
  // ─────────────────────────────────────────────────────────────

  loadPreviousCompletedJobs(holdJob: any) {
    this.loadingPrevJobs = true;

    // ← CHANGED: now calls GetAllCompletedJobs instead of GetCompletedQCJobs
    this.jobService.GetAllCompletedJobs()
      .pipe(finalize(() => this.loadingPrevJobs = false))
      .subscribe({
        next: (res: any) => {
          let jobs: any[] = res.data ?? [];

          const jobNo    = (holdJob.jobNumber || '').trim();
          const serialNo = (holdJob.serialNo  || '').trim();

          // Filter: same job + same serial (all completed ops, any trans_type)
          jobs = jobs.filter((j: any) =>
            (j.jobNumber || '').trim() === jobNo &&
            (j.serialNo  || '').trim() === serialNo
          );

          // Group so we get one card per unique operation+wc+transType
          const grouped = this.groupAllCompleted(jobs);

          // Sort descending by operation number
          grouped.sort((a, b) => Number(b.operationNumber) - Number(a.operationNumber));

          this.previousCompletedJobs = grouped;
        },
        error: (err) => {
          console.error('Failed to load completed jobs:', err);
          this.previousCompletedJobs = [];
        }
      });
  }

  selectPrevJob(prev: any) {
    this.selectedPrevJob = prev;
    this.nowLabel = new Date().toLocaleString();
  }

  closeReopenDialog() {
    this.showReopenDialog      = false;
    this.selectedHoldJob       = null;
    this.selectedPrevJob       = null;
    this.previousCompletedJobs = [];
  }

  // ─────────────────────────────────────────────────────────────
  // Submit Reopen
  // Step 1: startQCJob on the hold/rejected job → new START tx
  // Step 2: SubmitReopenHoldJob → save track record
  // Step 3: Refresh tabs, switch to track tab
  // ─────────────────────────────────────────────────────────────

  submitReopen() {
    if (!this.selectedHoldJob || !this.selectedPrevJob) return;

    const localDateTime = this.localNow();
    const ud            = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const emp           = ud.employeeCode || '';

    // Payload to create a new START transaction for the hold/rejected job
    const startPayload = {
      JobNumber:       this.selectedHoldJob.jobNumber,
      SerialNo:        this.selectedHoldJob.serialNo,
      OperationNumber: this.selectedHoldJob.operationNumber,
      Wc:              this.selectedHoldJob.wcCode,
      Item:            this.selectedHoldJob.item,
      EmpNum:          emp,
      loginuser:       emp,
      startTime:       localDateTime
    };

    // Track record saved to JobReopenTrack table
    const trackPayload = {
      holdJobNumber:       this.selectedHoldJob.jobNumber,
      holdSerialNo:        this.selectedHoldJob.serialNo,
      holdOperationNumber: this.selectedHoldJob.operationNumber,
      holdWc:              this.selectedHoldJob.wcCode,
      holdItem:            this.selectedHoldJob.item,
      holdStatus:          this.reopenSource === 'reject' ? 'REJECTED' : 'HOLD',

      prevJobNumber:       this.selectedPrevJob.jobNumber,
      prevSerialNo:        this.selectedPrevJob.serialNo,
      prevOperationNumber: this.selectedPrevJob.operationNumber,
      prevWc:              this.selectedPrevJob.wcCode,
      prevItem:            this.selectedPrevJob.item,
      prevStatus:          'COMPLETED',

      submittedBy:  emp,
      submittedOn:  localDateTime
    };

    this.submittingReopen = true;
    this.loader.show();

    // Step 1: Create new START transaction for hold/rejected job
    this.jobService.startQCJob(startPayload)
      .subscribe({
        next: (startRes: any) => {

          if (startRes?.trans_num) {
            (trackPayload as any).newTransNum = startRes.trans_num;
          }

          // Step 2: Save track record
          this.jobService.SubmitReopenHoldJob(trackPayload)
            .pipe(finalize(() => {
              this.submittingReopen = false;
              this.loader.hide();
            }))
            .subscribe({
              next: () => {
                const isReject = this.reopenSource === 'reject';

                Swal.fire({
                  icon: 'success',
                  title: 'Job Reopened!',
                  html: `
                    <div style="text-align:left; font-size:13px; line-height:1.7;">
                      <b>${this.selectedHoldJob.jobNumber}</b>
                      (Serial: ${this.selectedHoldJob.serialNo}, Opr ${this.selectedHoldJob.operationNumber})
                      has been <strong style="color:#1565C0;">restarted</strong>.<br><br>
                      A new start transaction has been created.<br>
                      The job is now visible in the <b>On Going Jobs</b> tab.
                    </div>`,
                  confirmButtonText: 'OK',
                  confirmButtonColor: '#1565C0'
                });

                this.closeReopenDialog();

                if (isReject) {
                  this.loadRejectedJobs();
                  this.loadRejectSubmittedJobs();
                  this.activeTabIndex = 3;
                } else {
                  this.loadHoldJobs();
                  this.loadHoldSubmittedJobs();
                  this.activeTabIndex = 2;
                }
              },
              error: (err: any) => {
                Swal.fire('Error', err.error?.message || 'Failed to save track record', 'error');
              }
            });
        },
        error: (err: any) => {
          this.submittingReopen = false;
          this.loader.hide();
          Swal.fire('Error', err.error?.message || 'Failed to start the job', 'error');
        }
      });
  }
}