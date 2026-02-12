import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidenavComponent } from "../sidenav/sidenav";
import { HeaderComponent } from "../header/header";
import { JobService } from '../../services/job.service';

interface Job {
  jobId: string;
  serialNo: string;
  qcGroup: string;
  workCenter: string;
  operation: string | number;
  employee: string;
  lastCheck: string;
  qcStatus: string;
  statusColor: string;
  selectable?: boolean;
}


@Component({
  selector: 'app-qc-dashboard',
  templateUrl: './dashboard_qc.html',
  styleUrls: ['./dashboard_qc.scss'],
  standalone: true,
  imports: [SidenavComponent, HeaderComponent, CommonModule]
})
export class QcDashboardComponent {
  isSidebarHidden = window.innerWidth <= 1024;

 metrics = {
  runningQC: 0,
  pausedQC: 0,
  extendedQC: 0,
  completedQC: 0,
  goal: 0
};


  jobs: Job[] = [];

  currentPage = 1;
  pageSize = 10;
  totalRecords = 0;
  totalPages = 0;

  constructor(private jobService: JobService) {}

  ngOnInit() {
    this.checkScreenSize();
    this.loadQcOverview();
    this.loadQcJobs();
  }

     @HostListener('window:resize')
  onResize() {
    this.checkScreenSize();
  }

    checkScreenSize() {
    if (window.innerWidth <= 1024) {
      this.isSidebarHidden = true;   // Mobile → hidden
    } else {
      this.isSidebarHidden = false;  // Desktop → visible
    }
  }

  toggleSidebar() {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

 loadQcOverview(todayOnly: number = 0) {
  const payload = { todayOnly, includeTransaction: 0, includeQC: 1, includeVerify: 0 };

  this.jobService.GetTransactionOverview(payload).subscribe({
    next: (res: any) => {
      if (res.success && res.qcOverview) {
        const qc = res.qcOverview;

        // 🧭 Map QC overview API fields directly
        this.metrics = {
          runningQC: qc.runningQCJobs || 0,   // → Running QC Jobs
          pausedQC: qc.pausedQCJobs || 0,     // → Paused QC Jobs
          extendedQC: qc.extendedQCJobs || 0, // → Extended QC Jobs
          completedQC: qc.completedQCJobs || 0, // → Completed QC Jobs
          goal: 0 // optional goal value
        };
      }
    },
    error: (err) => console.error('Error loading QC overview:', err)
  });
}

loadQcJobs(todayOnly: number = 0) {
  const payload = {
    TodayOnly: todayOnly,
    IncludeTransaction: 0, //  Not transaction
    IncludeQC: 1,
    IncludeVerify: 0,            //  Only QC
    PageNumber: this.currentPage,
    PageSize: this.pageSize
  };

  this.jobService.GetTransactionData(payload).subscribe({
    next: (res: any) => {
      if (res.success) {
        this.jobs = (res.data || [])
          .filter((item: any) => item.type === 'QC')
          .map((item: any) => ({
            jobId: item.job || '-',
            serialNo: item.serialNo || '-',
            qcGroup: item.qcGroup || '-',
            workCenter: item.wc || '-',
            operation: item.operation || '-',
            employee: item.employee?.trim() || '-',
            lastCheck: item.time ? new Date(item.time).toLocaleString() : '-',
            qcStatus: this.getQcStatus(item.progress),
            statusColor: this.getStatusColor(item.progress)
          }));

        this.totalRecords = res.totalRecords || 0;
        this.totalPages = Math.ceil(this.totalRecords / this.pageSize);
      }
    },
    error: (err) => console.error('Error loading QC jobs:', err)
  });
}


  //  Map progress to friendly status
  getQcStatus(progress: string): string {
    switch ((progress || '').toLowerCase()) {
      case 'completed':
        return 'Passed';
      case 'paused':
        return 'In Review';
      case 'unknown':
        return 'Failed';
      default:
        return 'Ready';
    }
  }

  //  Status Color Mapping
  getStatusColor(progress: string): string {
    switch ((progress || '').toLowerCase()) {
      case 'completed':
        return '#10b981'; // green
      case 'paused':
        return '#f59e0b'; // yellow
      case 'unknown':
        return '#ef4444'; // red
      default:
        return '#3b82f6'; // blue (ready)
    }
  }

  //  Pagination Support (optional)
  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.loadQcJobs();
    }
  }

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.loadQcJobs();
    }
  }
}
