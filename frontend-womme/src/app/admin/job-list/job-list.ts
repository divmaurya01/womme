import { Component, OnInit ,ViewChild} from '@angular/core';
import { Router ,ActivatedRoute} from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { JobService } from '../../services/job.service';
import { TableModule, Table } from 'primeng/table';
import Swal from 'sweetalert2';
import { Subject } from 'rxjs';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';


@Component({
  selector: 'app-job-list',
  templateUrl: './job-list.html',
  styleUrls: ['./job-list.scss'],
  imports:[CommonModule,FormsModule,HeaderComponent,SidenavComponent,TableModule]
})
export class JobListComponent implements OnInit {

  jobs: any[] = [];
  loading = true;
  isSidebarHidden=false
  isLoading = false;
  syncMessage: string | null = null;
  isError = false;

  operations: any[] = []; // JobTranMst transactions
  totalRecords = 0;
  searchTerm = '';
    // Timer intervals per row
  timerIntervals: { [jobKey: string]: any } = {};
  jobTimers: { [jobKey: string]: string } = {};
  @ViewChild('dt') dt!: Table;
  dtTrigger: Subject<any> = new Subject();

  constructor(private router: Router, private route: ActivatedRoute,private jobService: JobService,private loader:LoaderService) {}

  ngOnInit(): void {
    this.loadJobsLazy();

  }
    // Load transactions
  loadJobsLazy(event?: any) {
    this.isLoading = true;
    const page = event?.first ? event.first / event?.rows : 0;
    const size = event?.rows || 5000;

    const userDetails = JSON.parse(localStorage.getItem("userDetails") || "{}");
    const loggedInEmpCode = userDetails.employeeCode;
    const roleId = userDetails.roleID;
    console.log(roleId)
    const empCodeParam = roleId === 1 ? null : loggedInEmpCode;
    console.log("employee is admin",empCodeParam)
    this.loader.show();
    this.jobService.GetUnpostedTransactions(page, size, this.searchTerm,loggedInEmpCode)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: res => {
        
        const grouped: { [job: string]: any[] } = {};

        // 1️⃣ Group rows by JOB
        res.data.forEach((row: any) => {
          if (!grouped[row.job]) {
            grouped[row.job] = [];
          }
          grouped[row.job].push(row);
        });

        // 2️⃣ Build ONE ROW PER JOB
        this.operations = Object.keys(grouped).map(jobKey => {
          const rows = grouped[jobKey];

          // pick first row for common fields
          const first = rows[0];

          return {
            job: first.job,
            item: first.item,
            empName: first.empName,
            wc: first.wcDescription,

            // combine logs if available
            logDetails: rows.flatMap(r => r.logDetails || []),

            // for timer key
            jobKey: first.job
          };
        });

        this.totalRecords = this.operations.length;
        this.isLoading = false;

        this.totalRecords = res.total;
        this.isLoading = false;

        this.operations.forEach(job => {
          const jobKey = this.getJobKey(job);
          this.startJobTimer(jobKey, job.logDetails || []);
        });
      },
      error: err => {
        this.isLoading = false;
        Swal.fire('Error', 'Failed to fetch job transactions', 'error');
      }
    });
  }
   getJobKey(job: any): string {
      return job.job;
    }

    startJobTimer(jobKey: string, logs: any[]) {
    // Clear existing timer
    if (this.timerIntervals[jobKey]) {
      clearInterval(this.timerIntervals[jobKey]);
    }

    // Initial calculation
    this.jobTimers[jobKey] = this.calculateElapsedTime(logs);

    this.timerIntervals[jobKey] = setInterval(() => {
      this.jobTimers[jobKey] = this.calculateElapsedTime(logs);
    }, 1000);
  }
    calculateElapsedTime(logs: any[]): string {
    if (!logs || logs.length === 0) return '00:00:00';

    let totalMs = 0;
    let runningStart: number | null = null;
    const now = new Date(this.jobService.getLocalDateTime()).getTime();

    logs.forEach(log => {
      const logTime = new Date(log.statusTime).getTime();
      if (log.statusID === 1) {
        runningStart = logTime;
      } else if ((log.statusID === 2 || log.statusID === 3) && runningStart !== null) {
        totalMs += logTime - runningStart;
        runningStart = null;
      }
    });

    if (runningStart !== null) {
      totalMs += now - runningStart;
    }

    const totalSeconds = Math.floor(totalMs / 1000);
    const hours = Math.floor(totalSeconds / 3600).toString().padStart(2, '0');
    const minutes = Math.floor((totalSeconds % 3600) / 60).toString().padStart(2, '0');
    const seconds = (totalSeconds % 60).toString().padStart(2, '0');

    return `${hours}:${minutes}:${seconds}`;
  }
//  viewReport(jobNumber: string): void {
//     const currentParams = this.route.snapshot.queryParams;
//     const ss_id = currentParams['ss_id'] || '';

//     this.router.navigate(['/reports'], {
//       queryParams: {
//         ss_id: ss_id,
//       jobId: jobNumber   // ✅ match ReportsView
//       }
//     });
//   }
    toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }
  onJobClick(job: string, ): void {
    const currentParams = this.route.snapshot.queryParams;
    const ss_id = currentParams['ss_id'] || '';

    this.router.navigate(['/reports'], {
      queryParams: {
        ss_id: ss_id,
        jb_id: job,
       
      }
    });
  }
    

  onGlobalSearch(event: Event): void {
    const input = (event.target as HTMLInputElement).value
      .toLowerCase()
      .trim();

    if (!input) {
      this.dt.clear();
      return;
    }

    // multiple keywords support: a|b|c
    const keywords = input.split('|').map(k => k.trim());

    this.dt.filterGlobal(keywords.join(' '), 'contains');
  }





    ngAfterViewInit(): void {
    this.dtTrigger.next(true);
  }

  ngOnDestroy(): void {
    
    this.dtTrigger.unsubscribe();
    this.stopAllTimers();
  }
   stopAllTimers() {
    Object.keys(this.timerIntervals).forEach(key => clearInterval(this.timerIntervals[key]));
    this.timerIntervals = {};
  }
  getJobStatus(logs: any[]): string {
    if (!logs || logs.length === 0) return '-';
    const lastLog = logs[logs.length - 1];
    return lastLog.statusID === 1 ? 'Started' :
           lastLog.statusID === 2 ? 'Paused' :
           lastLog.statusID === 3 ? 'Completed' : '-';
  }
   syncJob() {
    this.isLoading = true;
    this.syncMessage = null;
    this.isError = false;
    this.loader.show();
    this.jobService.SyncJobTranMst()
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res: any) => {
        this.isLoading = false;
        this.syncMessage = res; // backend returns string
        this.isError = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.syncMessage = err.error || "Something went wrong while syncing.";
        this.isError = true;
      }
    });
  }
}
