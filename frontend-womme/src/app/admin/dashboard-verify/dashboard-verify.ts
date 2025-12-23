import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidenavComponent } from "../sidenav/sidenav";
import { HeaderComponent } from "../header/header";
import { JobService } from '../../services/job.service';

interface Job {
  jobId: string;
  serialNo: string;
  workCenter: string;
  operation: string | number;
  employee: string;
  lastCheck: string;
  verifyStatus: string;
  statusColor: string;
}

@Component({
  selector: 'app-dashboard-verify',
  templateUrl: './dashboard-verify.html',
  styleUrl: './dashboard-verify.scss',
  standalone: true,
  imports: [SidenavComponent, HeaderComponent, CommonModule]
})
export class DashboardVerify {

  isSidebarHidden = false;

  /** VERIFY METRICS */
  metrics = {
    runningVerify: 0,
    pausedVerify: 0,
    extendedVerify: 0,
    completedVerify: 0,
    goal: 0
  };

  jobs: Job[] = [];

  currentPage = 1;
  pageSize = 10;
  totalRecords = 0;
  totalPages = 0;

  constructor(private jobService: JobService) {}

  ngOnInit() {
    this.loadVerifyOverview();
    this.loadVerifyJobs();
  }

  toggleSidebar() {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  // ================= VERIFY OVERVIEW =================
  loadVerifyOverview(todayOnly: number = 0) {
    const payload = {
      todayOnly,
      includeTransaction: 0,
      includeQC: 0,
      includeVerify: 1
    };

    this.jobService.GetTransactionOverview(payload).subscribe({
      next: (res: any) => {
        if (res.success && res.verifyOverview) {
          const v = res.verifyOverview;

          this.metrics = {
            runningVerify: v.runningVerifyJobs || 0,
            pausedVerify: v.pausedVerifyJobs || 0,
            extendedVerify: v.extendedVerifyJobs || 0,
            completedVerify: v.completedVerifyJobs || 0,
            goal: 0
          };
        }
      },
      error: err => console.error('Error loading VERIFY overview:', err)
    });
  }

  // ================= VERIFY JOB LIST =================
  loadVerifyJobs(todayOnly: number = 0) {
    const payload = {
      TodayOnly: todayOnly,
      IncludeTransaction: 0,
      IncludeQC: 0,
      IncludeVerify: 1,
      PageNumber: this.currentPage,
      PageSize: this.pageSize
    };

    this.jobService.GetTransactionData(payload).subscribe({
      next: (res: any) => {
        if (res.success) {
          this.jobs = (res.data || [])
            .filter((item: any) => item.wc === 'VERIFY')
            .map((item: any) => ({
              jobId: item.job || '-',
              serialNo: item.serialNo || '-',
              workCenter: item.wc || '-',
              operation: item.operation || '-',
              employee: item.employee?.trim() || '-',
              lastCheck: item.time
                ? new Date(item.time).toLocaleString()
                : '-',
              verifyStatus: this.getVerifyStatus(item.progress),
              statusColor: this.getStatusColor(item.progress)
            }));

          this.totalRecords = res.totalRecords || 0;
          this.totalPages = Math.ceil(this.totalRecords / this.pageSize);
        }
      },
      error: err => console.error('Error loading VERIFY jobs:', err)
    });
  }

  // ================= STATUS HELPERS =================
  getVerifyStatus(progress: string): string {
    switch ((progress || '').toLowerCase()) {
      case 'completed':
        return 'Verified';
      case 'paused':
        return 'On Hold';
      case 'running':
        return 'Verifying';
      default:
        return 'Pending';
    }
  }

  getStatusColor(progress: string): string {
    switch ((progress || '').toLowerCase()) {
      case 'completed':
        return '#10b981'; // green
      case 'paused':
        return '#f59e0b'; // yellow
      case 'running':
        return '#3b82f6'; // blue
      default:
        return '#ef4444'; // red
    }
  }

  // ================= PAGINATION =================
  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.loadVerifyJobs();
    }
  }

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.loadVerifyJobs();
    }
  }
}
