
import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidenavComponent } from "../sidenav/sidenav";
import { HeaderComponent } from "../header/header";
import { JobService } from '../../services/job.service';

@Component({
  selector: 'app-production-qc-dashboard',  
  templateUrl: './dashboard_emp.html',
  styleUrls: ['./dashboard_emp.scss'],
  standalone: true,
  imports: [SidenavComponent, HeaderComponent, CommonModule]
})
export class ProductionQcDashboardComponent {
  selectedCard: string = '';
     isSidebarHidden = window.innerWidth <= 1024;

  // ✅ Data objects
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

  cards: any[] = [];
  jobData: any[] = [];
currentPage = 1;
  pageSize = 10;
  totalRecords = 0;
  totalPages = 0;
  hoveredRow: number = -1;

  constructor(private jobService: JobService) {}

  ngOnInit() {
    this.checkScreenSize();
    this.loadOverview();
    this.loadUtilizationData();
    this.loadTransactionData();  // ✅ also load job table data
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

  selectCard(cardType: string) {
    this.selectedCard = this.selectedCard === cardType ? '' : cardType;
  }

  //  Load overview (Production & QC)
  loadOverview(todayOnly: number = 0, includeTransaction: number = 1, includeQC: number = 1, includeVerify: number = 1) {
    const payload = { todayOnly, includeTransaction, includeQC, includeVerify };

    this.jobService.GetTransactionOverview(payload).subscribe({
      next: (res: any) => {
        if (res.success) {
          this.transaction = res.transactionOverview || this.transaction;
          this.qc = res.qcOverview || this.qc;
          this.updateCards(); // Refresh UI cards
        }
      },
      error: (err) => console.error('Error loading overview:', err)
    });
  }

  //  Load Employee/Machine stats
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
// ✅ Load job transaction data for the job table
loadTransactionData(todayOnly: number = 0, includeTransaction: number = 1, includeQC: number = 0) {
  const payload = {
    TodayOnly: todayOnly,
    IncludeTransaction: includeTransaction,
    IncludeQC: includeQC,
    IncludeVerify: 1,  
    PageNumber: this.currentPage,
    PageSize: this.pageSize
  };

  this.jobService.GetTransactionData(payload).subscribe({
    next: (res: any) => {
      if (res.success) {
        // 🧩 Map response fields to table-friendly format
        this.jobData = (res.data || []).map((item: any) => ({
          job: item.job || '-',
          serialNo: item.serialNo || '-',
          machine: item.machine || '-',
          operation: item.operation || '-',
          employee: item.employee || '-',
          time: item.time ? new Date(item.time) : null,
          progress: item.progress || '-'
        }));

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
  //  Card setup
  updateCards() {
    this.cards = [
      { type: 'running', title: 'JOBS RUNNING', value: this.transaction.runningJobs, small: '', borderColor: '#22c55e' },
      { type: 'paused', title: 'JOBS PAUSED', value: this.transaction.pausedJobs, small: '', borderColor: '#f97316' },
      { type: 'qc', title: 'JOBS IN QC REVIEW', value: this.qc.runningQCJobs, small: 'critical check', borderColor: '#3b82f6' },
      { type: 'extended', title: 'EXTENDED (OT)', value: this.transaction.extendedJobs, small: '', borderColor: '#ef4444' },
      { type: 'completed', title: 'COMPLETED (TODAY)', value: this.transaction.completedJobs, small: '', borderColor: '#6b7280' },
    ];
  }

  //  Idle Resource Calculations
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

