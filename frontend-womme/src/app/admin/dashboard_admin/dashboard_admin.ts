import { Component } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { SidenavComponent } from "../sidenav/sidenav";
import { HeaderComponent } from "../header/header";
import { JobService } from '../../services/job.service';

@Component({
  selector: 'app-dashboard-overview',
  templateUrl: './dashboard_admin.html',
  styleUrls: ['./dashboard_admin.scss'],
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    SidenavComponent,
    HeaderComponent
  ]
})
export class DashboardOverviewComponent {
  isSidebarHidden = false;

  transaction = {
    runningJobs: 0,
    pausedJobs: 0,
    extendedJobs: 0,
    completedJobs: 0
  };

  qc = {
    runningQCJobs: 0,
    pausedQCJobs: 0,
    extendedQCJobs: 0,
    completedQCJobs: 0
  };

  utilization = {
    totalEmployees: 0,
    activeEmployees: 0,
    employeeUtilization: "0%",
    totalMachines: 0,
    activeMachines: 0,
    machineUtilization: "0%"
  };

  jobData: any[] = [];
  currentPage = 1;
  pageSize = 10;
  totalRecords = 0;
  totalPages = 0;
  hoveredRow: number = -1;

  constructor(private jobService: JobService) {}

  ngOnInit() {
    this.loadOverview();
    this.loadTransactionData();
    this.loadUtilizationData(); // ✅ new call
  }

  toggleSidebar() {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  loadOverview(todayOnly: number = 0, includeTransaction: number = 1, includeQC: number = 1) {
    const payload = { todayOnly, includeTransaction, includeQC };
    this.jobService.GetTransactionOverview(payload).subscribe({
      next: (res: any) => {
        if (res.success) {
          this.transaction = res.transactionOverview || this.transaction;
          this.qc = res.qcOverview || this.qc;
        }
      },
      error: (err) => console.error('Error loading overview:', err)
    });
  }

  loadUtilizationData() {
    this.jobService.GetEmployeeAndMachineStats().subscribe({
      next: (res: any) => {
        if (res.success && res.data) {
          this.utilization = res.data;
        }
      },
      error: (err) => console.error('Error loading utilization data:', err)
    });
  }

  loadTransactionData(todayOnly: number = 0, includeTransaction: number = 1, includeQC: number = 1) {
    const payload = {
      TodayOnly: todayOnly,
      IncludeTransaction: includeTransaction,
      IncludeQC: includeQC,
      PageNumber: this.currentPage,
      PageSize: this.pageSize
    };

    this.jobService.GetTransactionData(payload).subscribe({
      next: (res: any) => {
        if (res.success) {
          this.jobData = res.data || [];
          this.totalRecords = res.totalRecords || 0;
          this.totalPages = Math.ceil(this.totalRecords / this.pageSize);
        }
      },
      error: (err) => console.error('Error loading job data:', err)
    });
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.loadTransactionData();
    }
  }

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.loadTransactionData();
    }
  }

  goToPage(page: number) {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.loadTransactionData();
  }

getIdlePercentageEmployee(): number {
  if (!this.utilization) return 0;

  const activeValue = this.utilization.employeeUtilization;
  const active = typeof activeValue === 'string'
    ? parseFloat(activeValue.replace('%', ''))
    : Number(activeValue) || 0;

  return Math.max(0, 100 - active);
}

getIdlePercentageMachine(): number {
  if (!this.utilization) return 0;

  const activeValue = this.utilization.machineUtilization;
  const active = typeof activeValue === 'string'
    ? parseFloat(activeValue.replace('%', ''))
    : Number(activeValue) || 0;

  return Math.max(0, 100 - active);
}


}
