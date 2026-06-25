import { Component, HostListener } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { SidenavComponent } from '../sidenav/sidenav';
import { HeaderComponent } from '../header/header';
import { JobService } from '../../services/job.service';
import { Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-dashboard-overview',
  templateUrl: './dashboard_admin.html',
  styleUrls: ['./dashboard_admin.scss'],
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    SidenavComponent,
    HeaderComponent,
    RouterModule
  ]
})
export class DashboardOverviewComponent {

  // ── Transaction / QC / Verify overviews ──────────────────────────────────
  transaction = {
    runningJobs: 0,
    pausedJobs: 0,
    normalCompletedJobs: 0,
    extendedJobs: 0,
    ongoingCriticalJobs: 0,
    nextOperationJobs: 0
  };

  qc = {
    runningQCJobs: 0,
    pausedQCJobs: 0,
    normalCompletedQCJobs: 0,
    extendedQCJobs: 0,
    ongoingCriticalQCJobs: 0,
    holdQCJobs: 0,
    rejectedQCJobs: 0,
    nextOperationQCJobs: 0
  };

  verify = {
    runningVerifyJobs: 0,
    pausedVerifyJobs: 0,
    normalCompletedVerifyJobs: 0,
    extendedVerifyJobs: 0,
    ongoingCriticalVerifyJobs: 0,
    nextOperationVerifyJobs: 0  
  };

  // ── Issue Operations counts ───────────────────────────────────────────────
  issueStats = {
    pendingIssues:   0,   // jobs in issued-transaction list (not yet done)
    completedIssues: 0    // completed issue jobs (status = 3, wc = ISSUE)
  };

  // ── Project Dashboard counts ──────────────────────────────────────────────
  projectStats = {
    holdJobs:        0,
    rejectedJobs:    0,
    holdSubmitted:   0,
    rejectSubmitted: 0
  };

  // ── Utilization ───────────────────────────────────────────────────────────
  utilization = {
    totalEmployees:      0,
    activeEmployees:     0,
    employeeUtilization: '0%',
    totalMachines:       0,
    activeMachines:      0,
    machineUtilization:  '0%'
  };

  // ── Job table ─────────────────────────────────────────────────────────────
  allJobData:   any[] = [];
  jobData:      any[] = [];
  currentPage  = 1;
  pageSize     = 10;
  totalRecords = 0;
  totalPages   = 0;
  hoveredRow: number = -1;
  roleId: number = 0;

  isSidebarHidden = window.innerWidth <= 1024;

  constructor(private jobService: JobService, private router: Router) {}

  ngOnInit() {
    this.checkScreenSize();

    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    this.roleId = userDetails.roleID;

    this.applyRoleRules();
    this.loadUtilizationData();
    this.loadProjectStats();
    this.loadIssueStats();   // ← NEW
  }

  @HostListener('window:resize')
  onResize() { this.checkScreenSize(); }

  checkScreenSize() { this.isSidebarHidden = window.innerWidth <= 1024; }
  toggleSidebar()   { this.isSidebarHidden = !this.isSidebarHidden; }

  // ─────────────────────────────────────────────────────────────────────────
  // Role-based loading
  // ─────────────────────────────────────────────────────────────────────────

  applyRoleRules() {
    if (this.roleId === 4) {
      this.loadOverview(0, 1, 0, 0);
      this.loadTransactionData(0, 1, 0, 0);
    } else if (this.roleId === 5) {
      this.loadOverview(0, 0, 1, 0);
      this.loadTransactionData(0, 0, 1, 0);
    } else {
      this.loadOverview();
      this.loadTransactionData();
    }
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Overview APIs
  // ─────────────────────────────────────────────────────────────────────────

  loadOverview(
    todayOnly: number = 0,
    includeTransaction: number = 1,
    includeQC: number = 1,
    includeVerify: number = 1
  ) {
    const payload = { todayOnly, includeTransaction, includeQC, includeVerify };
    this.jobService.GetTransactionOverview(payload).subscribe({
      next: (res: any) => {
        if (res.success) {
          this.transaction = res.transactionOverview ?? this.transaction;
          this.qc          = res.qcOverview          ?? this.qc;
          this.verify = { ...this.verify, ...(res.verifyOverview ?? {}) };
        }
      },
      error: err => console.error('Error loading overview:', err)
    });
  }

  loadUtilizationData() {
    this.jobService.GetEmployeeAndMachineStats().subscribe({
      next: (res: any) => {
        if (res.success && res.data) this.utilization = res.data;
      },
      error: err => console.error('Error loading utilization:', err)
    });
  }

  // ─────────────────────────────────────────────────────────────────────────
  // NEW: Issue Operations stats
  // Pending  = jobs still in the issued-transaction list (GetIssuedTransactions)
  // Completed = issue jobs where latest status = "3" from GetAllCompletedJobs
  // Both fire in parallel via forkJoin
  // ─────────────────────────────────────────────────────────────────────────

 loadIssueStats() {
  forkJoin({
    pending:   this.jobService.GetIssuedTransactions(0, 99999, '', ''),
    completed: this.jobService.GetAllCompletedJobs(),
    verify:    this.jobService.getVerifyTransactions(0, 99999, '', ''),
    nextOp:    this.jobService.getIsNextJobActive()   // ← ADD
  }).subscribe({
    next: ({ pending, completed, verify, nextOp }: any) => {
      const pendingData   = (pending?.data   ?? []) as any[];
      const completedData = (completed?.data ?? []) as any[];
      const verifyData    = (verify?.data    ?? []) as any[];
      const nextOpData    = (nextOp?.data    ?? []) as any[];

      this.issueStats.pendingIssues = pendingData.length;

      this.issueStats.completedIssues = completedData.filter((j: any) =>
        (j.wcCode ?? j.wc ?? '').toLowerCase().includes('issue')
      ).length;

      // Build job|serial → [nextOper...] map, same pattern as Unposted page
      const nextOpMap = new Map<string, number[]>();
      nextOpData.forEach((x: any) => {
        const key = `${x.job}|${x.serialNo}`;
        if (!nextOpMap.has(key)) nextOpMap.set(key, []);
        nextOpMap.get(key)!.push(Number(x.nextOper));
      });

      // In-queue verify jobs = no status yet AND this op is the next operation
      this.verify.nextOperationVerifyJobs = verifyData.filter((j: any) => {
        const hasNoStatus = !j.status || j.status === '';
        const key = `${j.job}|${j.serialNo}`;
        const nextOps = nextOpMap.get(key) ?? [];
        const isNextOp = nextOps.includes(Number(j.operNum));
        return hasNoStatus && isNextOp;
      }).length;
    },
    error: err => console.error('Error loading issue stats:', err)
  });
}

  // ─────────────────────────────────────────────────────────────────────────
  // Project Dashboard counts
  // ─────────────────────────────────────────────────────────────────────────

  loadProjectStats() {
    forkJoin({
      hold:         this.jobService.GetHoldQCJobs(),
      rejected:     this.jobService.GetRejectedQCJobs(),
      holdSubmit:   this.jobService.GetHoldSubmittedJobs(),
      rejectSubmit: this.jobService.GetRejectSubmittedJobs()
    }).subscribe({
      next: ({ hold, rejected, holdSubmit, rejectSubmit }: any) => {
        this.projectStats = {
          holdJobs:        (hold?.data        ?? []).length,
          rejectedJobs:    (rejected?.data    ?? []).length,
          holdSubmitted:   (holdSubmit?.data  ?? []).length,
          rejectSubmitted: (rejectSubmit?.data ?? []).length
        };
      },
      error: err => console.error('Error loading project stats:', err)
    });
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Transaction table
  // ─────────────────────────────────────────────────────────────────────────

  loadTransactionData(
    todayOnly: number = 0,
    includeTransaction: number = 1,
    includeQC: number = 1,
    includeVerify: number = 1
  ) {
    const payload = {
      TodayOnly:          todayOnly,
      IncludeTransaction: includeTransaction,
      IncludeQC:          includeQC,
      IncludeVerify:      includeVerify,
      PageNumber:         1,
      PageSize:           99999
    };

    this.jobService.GetTransactionData(payload).subscribe({
      next: (res: any) => {
        if (res.success) {
          this.allJobData   = res.data || [];
          this.totalRecords = this.allJobData.length;
          this.totalPages   = Math.ceil(this.totalRecords / this.pageSize);
          this.currentPage  = 1;
          this.slicePage();
        }
      },
      error: err => console.error('Error loading job data:', err)
    });
  }

  slicePage() {
    const start  = (this.currentPage - 1) * this.pageSize;
    this.jobData = this.allJobData.slice(start, start + this.pageSize);
  }

  nextPage() { if (this.currentPage < this.totalPages) { this.currentPage++; this.slicePage(); } }
  prevPage() { if (this.currentPage > 1)               { this.currentPage--; this.slicePage(); } }

  goToPage(page: number) {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.slicePage();
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Idle helpers
  // ─────────────────────────────────────────────────────────────────────────

  getIdlePercentageEmployee(): number {
    if (!this.utilization) return 0;
    const a = typeof this.utilization.employeeUtilization === 'string'
      ? parseFloat(this.utilization.employeeUtilization.replace('%', ''))
      : Number(this.utilization.employeeUtilization) || 0;
    return Math.max(0, 100 - a);
  }

  getIdlePercentageMachine(): number {
    if (!this.utilization) return 0;
    const a = typeof this.utilization.machineUtilization === 'string'
      ? parseFloat(this.utilization.machineUtilization.replace('%', ''))
      : Number(this.utilization.machineUtilization) || 0;
    return Math.max(0, 100 - a);
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Status badge
  // ─────────────────────────────────────────────────────────────────────────

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Running':          return 'badge badge-success';
      case 'Paused':           return 'badge badge-warning';
      case 'Extended':         return 'badge badge-danger';
      case 'Ongoing Critical': return 'badge badge-dark';
      case 'Completed':        return 'badge badge-success';
      case 'Hold':             return 'badge badge-info';
      case 'Rejected':         return 'badge badge-danger';
      case 'In Queue':         return 'badge badge-primary';
      default:                 return 'badge badge-secondary';
    }
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Navigation
  // ─────────────────────────────────────────────────────────────────────────

  private getSsId(): string | null {
    return this.router.routerState.snapshot.root.queryParams['ss_id']
        || localStorage.getItem('ss_id');
  }

  goToTransaction(type: string) {
    const ss_id = this.getSsId();
    if (!ss_id) { console.warn('ss_id missing'); return; }
    let route = '/unpostedjobtransaction';
    if (type === 'completed') {
      route = '/postedjobtransaction';
    } else if (type === 'extended') {
      route = (this.transaction.runningJobs > 0 || this.transaction.pausedJobs > 0)
        ? '/unpostedjobtransaction' : '/postedjobtransaction';
    }
    this.router.navigate([route], { queryParams: { ss_id, highlight: 'extended' } });
  }

  goToQC(type: string) {
    const ss_id = this.getSsId();
    if (!ss_id) { console.warn('ss_id missing'); return; }
    this.router.navigate(['/qualitychecker'], { queryParams: { status: type, ss_id } });
  }

  goToVerify(type: string) {
    const ss_id = this.getSsId();
    if (!ss_id) { console.warn('ss_id missing'); return; }
    this.router.navigate(['/verify-transaction'], { queryParams: { status: type, ss_id } });
  }

  // NEW: Navigate to issue transaction page
  goToIssue(type: 'pending' | 'completed') {
    const ss_id = this.getSsId();
    if (!ss_id) { console.warn('ss_id missing'); return; }
    // pending → issue transaction list, completed → posted transaction filtered
    const route = type === 'completed' ? '/postedjobtransaction' : '/issuejobtransaction';
    this.router.navigate([route], { queryParams: { ss_id } });
  }

  goToProjectDashboard(tab: 'hold' | 'reject' | 'holdSubmit' | 'rejectSubmit') {
    const ss_id = this.getSsId();
    if (!ss_id) { console.warn('ss_id missing'); return; }
    this.router.navigate(['/project-dashboard'], { queryParams: { status: tab, ss_id } });
  }
}