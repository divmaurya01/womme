import { Component, HostListener } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { SidenavComponent } from "../sidenav/sidenav";
import { HeaderComponent } from "../header/header";
import { JobService } from '../../services/job.service';
import { Router, RouterModule } from '@angular/router';



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
    ongoingCriticalVerifyJobs: 0
  };



  utilization = {
    totalEmployees: 0,
    activeEmployees: 0,
    employeeUtilization: "0%",
    totalMachines: 0,
    activeMachines: 0,
    machineUtilization: "0%"
  };

  allJobData: any[] = [];     // full dataset loaded once
  jobData:    any[] = [];     // current page slice
  currentPage = 1;
  pageSize    = 10;
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

  applyRoleRules() {
    if (this.roleId === 4) {      
      this.loadOverview(0, 1, 0, 0);
      this.loadTransactionData(0, 1, 0, 0);
    }
    else if (this.roleId === 5) {      
      this.loadOverview(0, 0, 1, 0);
      this.loadTransactionData(0, 0, 1, 0);
    }
    else {      
      this.loadOverview();
      this.loadTransactionData();
    }
  }


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
            this.qc = res.qcOverview ?? this.qc;
            this.verify = res.verifyOverview ?? this.verify;
          }
        },
        error: err => console.error('Error loading overview:', err)
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

  loadTransactionData(todayOnly: number = 0, includeTransaction: number = 1, includeQC: number = 1, includeVerify: number = 1) {
    const payload = {
      TodayOnly: todayOnly,
      IncludeTransaction: includeTransaction,
      IncludeQC: includeQC,
      IncludeVerify: includeVerify,  
      PageNumber: 1,
      PageSize: 99999   // ← load everything once
      
    };

    this.jobService.GetTransactionData(payload).subscribe({
      next: (res: any) => {
        if (res.success) {
          this.allJobData  = res.data || [];
          this.totalRecords = this.allJobData.length;
          this.totalPages   = Math.ceil(this.totalRecords / this.pageSize);
          this.currentPage  = 1;
          this.slicePage();   // render first page instantly
        }
      },
      error: (err) => console.error('Error loading job data:', err)
    });
  }

  // ── Instant client-side slice — no API call ───────────────────────────
  slicePage() {
    const start = (this.currentPage - 1) * this.pageSize;
    this.jobData = this.allJobData.slice(start, start + this.pageSize);
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.slicePage();   // ← instant, no API
    }
  }

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.slicePage();   // ← instant, no API
    }
  }

  goToPage(page: number) {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.slicePage();
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

getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'Running':          return 'badge badge-success';
    case 'Paused':           return 'badge badge-warning';
    case 'Extended':         return 'badge badge-danger';
    case 'Ongoing Critical': return 'badge badge-dark';
    case 'Completed':        return 'badge badge-success';
    case 'Hold':             return 'badge badge-info';
    case 'Rejected':         return 'badge badge-danger';
    case 'Next Operation':   return 'badge badge-primary';  // ← add
    default:                 return 'badge badge-secondary';
  }
}

goToTransaction(type: string) {
  const ss_id =
    this.router.routerState.snapshot.root.queryParams['ss_id']
    || localStorage.getItem('ss_id');

  if (!ss_id) {
    console.warn('ss_id missing');
    return;
  }

  let route = '/unpostedjobtransaction'; // default

  if (type === 'completed') {
    route = '/postedjobtransaction';
  } else if (type === 'extended') {
    // Check actual running or paused jobs
    if ((this.transaction.runningJobs ?? 0) > 0 || (this.transaction.pausedJobs ?? 0) > 0) {
      route = '/unpostedjobtransaction';
    } else if ((this.transaction.normalCompletedJobs ?? 0) > 0) {
      route = '/postedjobtransaction';
    } else {
      route = '/unpostedjobtransaction';
    }
  }

  console.log('Transaction Overview:', this.transaction);
  console.log('➡️ Navigating to route:', route);

    this.router.navigate([route], { 
    queryParams: { 
      ss_id,
      highlight: 'extended'   // 👈 IMPORTANT
    } 
  });
}



goToQC(type: string) {

  const ss_id =
    this.router.routerState.snapshot.root.queryParams['ss_id']
    || localStorage.getItem('ss_id');

  if (!ss_id) {
    console.warn('ss_id missing');
    return;
  }

  let route = '/qualitychecker'; // QC page

  switch (type) {

    case 'running':
    case 'paused':
    case 'critical':
    case 'extended':
    case 'completed':
    case 'hold':
    case 'reject':
      route = '/qualitychecker';
      break;

    default:
      route = '/qualitychecker';
  }

  console.log('QC Overview:', this.qc);
  console.log('Navigating to QC route:', route);
  console.log('Status:', type);

  this.router.navigate([route], {
    queryParams: {
      status: type,
      ss_id
    }
  });

}

goToVerify(type: string) {
  const ss_id =
    this.router.routerState.snapshot.root.queryParams['ss_id']
    || localStorage.getItem('ss_id');

  if (!ss_id) {
    console.warn(' ss_id missing');
    return;
  }

  let route = '/verify-transaction'; // default

  if (type === 'completed') {
    route = '/verify-transaction';
  } 
  else if (type === 'extended') {
    if ((this.verify.runningVerifyJobs ?? 0) > 0 || (this.verify.pausedVerifyJobs ?? 0) > 0) {
      route = '/verify-transaction';
    } 
    else if ((this.verify.normalCompletedVerifyJobs ?? 0) > 0) {
      route = '/verify-transaction';
    } 
    else {
      route = '/verify-transaction';
    }
  }

  console.log('Verify Overview:', this.verify);
  console.log(' Navigating to Verify route:', route);

  this.router.navigate([route], {
    queryParams: { status: type, ss_id }
  });
}





}
