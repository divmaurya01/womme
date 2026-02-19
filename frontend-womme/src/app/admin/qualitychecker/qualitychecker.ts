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
export class QualityChecker implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('dt') dt!: Table;
    @ViewChild('tabView') tabView!: TabView;  // <-- new ViewChild
  activeTabIndex: number = 0;
  dtTrigger: Subject<any> = new Subject();

  transactions: any[] = [];
  totalRecords: number = 0;
  page: number = 0;
  size: number = 5000;
  searchTerm: string = '';
  isLoading: boolean = false;
 isSidebarHidden = window.innerWidth <= 1024;
  selectedJobs: any[] = [];
  activeJobTrans: any;
  ongoingJobs: any[] = [];
  completedJobs: any[] = [];
  scrappedJobs: any[] = [];
  totalScrappedRecords: number = 0;
  loggedInUser: string = '';
  jobTimers: { [key: string]: any } = {};
isExtendedMode: boolean = false;

  constructor(
    private jobService: JobService,
    private router: Router,
    private route: ActivatedRoute,
    private loader: LoaderService,
    private ngZone: NgZone
  ) {}

  ngOnInit(): void {
    this.checkScreenSize();
      this.route.queryParams.subscribe(params => {
      const status = params['status'];
      if (status === 'running') {
        this.activeTabIndex = 1; // On Going Jobs tab
        
      } 
       else if (status === 'paused') {
        this.activeTabIndex = 1;
       }

        else if (status === 'critical') {
        this.activeTabIndex = 1;
       }
     else if (status === 'extended') {
        this.activeTabIndex = 2;
        this.isExtendedMode = true;
       }
      else if (status === 'completed') {
        this.activeTabIndex = 2; // Completed Jobs tab
      } else if (status === 'scrapped') {
        this.activeTabIndex = 3; // Scrapped Jobs tab
      } else {
        this.activeTabIndex = 0; // New Jobs
      }
    });
    this.loadJobs();
    this.loadOngoingJobs();
    this.loadCompletedJobs();
    this.loadScrappedJobs();
  }

  ngAfterViewInit(): void {
    this.dtTrigger.next(true);
  }

  ngOnDestroy(): void {
    Object.values(this.jobTimers).forEach(timer => clearInterval(timer));
    this.dtTrigger.unsubscribe();
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
  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  onSearchChange(value: string) {
    this.searchTerm = value;
    this.loadJobs({ first: 0, rows: this.size });
  }

  loadJobs(pageEvent?: any) {
  this.isLoading = true;
  

  const page = pageEvent?.first ? pageEvent.first / pageEvent.rows : this.page;
  const size = pageEvent?.rows ?? this.size;

  const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
  const roleid = Number(userDetails.roleID);


  // Fetch Active + QC + NextJobActive together
  this.loader.show();
  this.jobService.GetActiveQCJobs().subscribe({
    next: (activeRes: any) => {
      const activeJobs = activeRes?.data ?? [];

      this.jobService.GetQC(page, size, this.searchTerm).pipe( finalize(() => { this.loader.hide(); })).subscribe({
        next: (qcRes: any) => {
          const qcJobs = qcRes?.data ?? [];

          this.jobService.getIsNextJobActive().subscribe({
            next: (nextRes: any) => {

              
              // Build NEXT OP MAP
             
              const nextOpMap = new Map<string, number[]>();

              (nextRes.data ?? []).forEach((x: any) => {
                const key = `${x.job}|${x.serialNo}`;
                if (!nextOpMap.has(key)) nextOpMap.set(key, []);
                nextOpMap.get(key)!.push(Number(x.nextOper));
              });

              
              // FINAL FILTER (ALL CONDITIONS)
              
              let finalJobs = qcJobs;

              if (roleid !== 1) {
                finalJobs = qcJobs
                  // Remove already active jobs
                  .filter((job: any) => {
                    return !activeJobs.some((active: any) =>
                      active.job === job.job &&
                      active.serialNo === job.serialNo &&
                      active.oper_num === +job.operNum &&
                      active.wc === job.wcCode &&
                      active.item === job.item
                    );
                  })
                  // Allow only next operation
                  .filter((job: any) => {
                    const key = `${job.job}|${job.serialNo}`;
                    const nextOps = nextOpMap.get(key) ?? [];
                    return nextOps.includes(Number(job.operNum));
                  });
              }

              // Common mapping for all roles
              this.transactions = finalJobs.map((x: any, index: number) => ({
                uniqueRowId: `${x.serialNo}-${x.operNum}-${x.wcCode}-${index}`,
                serialNo: x.serialNo.trim(),
                jobNumber: x.job.trim(),
                qtyReleased: x.qtyReleased,
                item: x.item.trim(),
                jobYear: x.jobYear,
                operationNumber: Number(x.operNum),
                wcCode: x.wcCode.trim(),
                wcDescription: x.wcDescription.trim(),
                empNum: x.emp_num,
                status: x.status,
                isActive: x.isActive
              }));

              this.totalRecords = this.transactions.length;


              this.isLoading = false;
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
  console.error('API Error:', err);

  this.isLoading = false;
  this.loader.hide();

  Swal.fire({
    icon: 'error',
    title: 'Error',
    text: err?.error?.message || 'Something went wrong. Please try again.',
  });
}

isExtendedJob(job: any): boolean {

  // 🔥 RULE — adjust as per your business logic

  // Example 1: based on shift hours
  if (job.total_a_hrs && job.total_a_hrs > 8) return true;

  // Example 2: based on logs
  if (job.allLogs && job.allLogs.length > 2) return true;

  // Example 3: status flag (if backend provides)
  if (job.status === 'EXTENDED') return true;

  return false;
}


  /** Start Single Job */
startQCJob(job: any) {
  const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
  const employeeCode = userDetails.employeeCode || '';

  const now = new Date();

  // ✅ Build LOCAL datetime string (no UTC conversion)
  const localDateTime =
    now.getFullYear() + '-' +
    String(now.getMonth() + 1).padStart(2, '0') + '-' +
    String(now.getDate()).padStart(2, '0') + 'T' +
    String(now.getHours()).padStart(2, '0') + ':' +
    String(now.getMinutes()).padStart(2, '0') + ':' +
    String(now.getSeconds()).padStart(2, '0');

  const payload = {
    JobNumber: job.jobNumber,
    SerialNo: job.serialNo,
    OperationNumber: job.operationNumber,
    Wc: job.wcCode,
    item: job.item,
    QtyReleased: job.qtyReleased,
    EmpNum: employeeCode,
    loginuser: employeeCode,
    startTime: localDateTime   // ✅ Local time, not UTC
  };

  Swal.fire({
    title: 'Start Job?',
    text: `Do you want to start Job ${job.jobNumber} (Serial ${job.serialNo}) at WC ${job.wcCode}?`,
    icon: 'question',
    showCancelButton: true,
    confirmButtonText: 'Yes, Start',
    cancelButtonText: 'Cancel'
  }).then((result) => {
    if (result.isConfirmed) {
      this.loader.show();
      this.jobService.startQCJob(payload)
        .pipe(finalize(() => { this.loader.hide(); }))
        .subscribe({
          next: () => {
            Swal.fire('Started!', `Job ${job.jobNumber} started successfully.`, 'success');
            this.loadJobs();
            this.loadOngoingJobs();
          },
          error: (err) =>
            Swal.fire('Error', err.error?.message || 'Failed to start job', 'error')
        });
    }
  });
}


  /** Start Group Jobs */
  StartGroupQCJobs() {
    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const employeeCode = userDetails.employeeCode || '';

    if (!this.selectedJobs || this.selectedJobs.length === 0) {
      Swal.fire('No jobs selected', 'Please select at least one job.', 'warning');
      return;
    }
    const now = new Date();

  const localDateTime =
    now.getFullYear() + '-' +
    String(now.getMonth() + 1).padStart(2, '0') + '-' +
    String(now.getDate()).padStart(2, '0') + 'T' +
    String(now.getHours()).padStart(2, '0') + ':' +
    String(now.getMinutes()).padStart(2, '0') + ':' +
    String(now.getSeconds()).padStart(2, '0');


    const payload = {
      jobs: this.selectedJobs.map(job => ({
        JobNumber: job.jobNumber,
        SerialNo: job.serialNo,
        OperationNumber: job.operationNumber,
        Wc: job.wcCode,
        Item: job.item,
        QtyReleased: job.qtyReleased,
        EmpNum: employeeCode,
        loginuser: employeeCode,
        StartTime: localDateTime 
      }))
    };

    Swal.fire({
      title: 'Start Selected Jobs?',
      text: `Do you want to start ${this.selectedJobs.length} selected jobs as a QC group?`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Start',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        this.loader.show();
        this.jobService.startGroupQCJobs(payload).pipe( finalize(() => { this.loader.hide(); })).subscribe({
          next: (res: any) => {
            const started = res.startedJobs?.length ?? 0;
            const skipped = res.skippedJobs?.length ?? 0;

            Swal.fire(
              'Jobs Processed',
              `${started} jobs started, ${skipped} skipped.`,
              skipped > 0 ? 'warning' : 'success'
            );

            this.loadJobs();
            this.loadOngoingJobs();
            this.selectedJobs = [];
          },
          error: (err) => Swal.fire('Error', err.error?.message || 'Failed to start selected jobs', 'error')
        });
      }
    });
  }

 

  loadOngoingJobs() {
  this.isLoading = true;
  this.loader.show();

  this.jobService.GetActiveQCJobs().subscribe({
    next: (res: any) => {
      const allJobs = res?.data ?? [];

      // Clear old timers
      Object.values(this.jobTimers).forEach(timer => clearInterval(timer));
      this.jobTimers = {};

      const activeJobs = allJobs.filter(
        (x: any) => x.status === "1" || x.status === "2"
      );

      this.ongoingJobs = activeJobs.map((x: any, index: number) => {

        const jobKey = `${x.job}-${x.serialNo}-${x.oper_num}-${x.wc}-${index}`;

        // Parse backend time AS-IS (local/server time)
        const startTime = new Date(x.start_time);
        const now = new Date();

        let elapsedSeconds = 0;

        // Calculate elapsed from backend start_time
        if (startTime) {
          elapsedSeconds = Math.floor(
            (now.getTime() - startTime.getTime()) / 1000
          );
        }

        // Add previously accumulated hours (resume case)
        if (x.total_a_hrs && x.total_a_hrs > 0) {
          elapsedSeconds += Math.floor(x.total_a_hrs * 3600);
        }

        // Safety
        if (elapsedSeconds < 0) elapsedSeconds = 0;

        const jobObj: any = {
          uniqueRowId: jobKey,
          jobNumber: x.job,
          serialNo: x.serialNo,
          operationNumber: x.oper_num,
          wcCode: x.wc,
          item: x.item,
          empNum: x.emp_num,
          emp_name: x.emp_name,
          trans_num: x.trans_num ?? x.trans_number ?? null,

          startTime,
          endTime: x.end_time ? new Date(x.end_time) : null,

          elapsedSeconds,
          elapsedTime: this.formatElapsedTime(elapsedSeconds),

          isPaused: x.status === "2",
          remark: x.remark ?? "",
          qcGroup: x.qcgroup ?? null,
          timerInterval: null
        };

        // ▶️ Start ticking ONLY if running
        if (x.status === "1") {
          jobObj.timerInterval = setInterval(() => {
            jobObj.elapsedSeconds++;
            jobObj.elapsedTime = this.formatElapsedTime(jobObj.elapsedSeconds);
          }, 1000);

          this.jobTimers[jobKey] = jobObj.timerInterval;
        }

        return jobObj;
      });

      this.isLoading = false;
      this.loader.hide();
    },
    error: err => {
      console.error('Error fetching active jobs:', err);
      this.isLoading = false;
      this.loader.hide();
    }
  });
}



  /** Format elapsed time */
 formatElapsedTime(totalSeconds: number): string {
    if (totalSeconds < 0) totalSeconds = 0; // safety guard

    const h = Math.floor(totalSeconds / 3600)
      .toString()
      .padStart(2, '0');

    const m = Math.floor((totalSeconds % 3600) / 60)
      .toString()
      .padStart(2, '0');

    const s = Math.floor(totalSeconds % 60)
      .toString()
      .padStart(2, '0');

    return `${h}:${m}:${s}`;
  }

  formatTime(hours: number): string {
    if (!hours || hours <= 0) return "00:00:00";

    const totalSeconds = Math.floor(hours * 3600);

    const h = Math.floor(totalSeconds / 3600).toString().padStart(2, '0');
    const m = Math.floor((totalSeconds % 3600) / 60).toString().padStart(2, '0');
    const s = Math.floor(totalSeconds % 60).toString().padStart(2, '0');

    return `${h}:${m}:${s}`;
  }



  /** Toggle Pause / Resume */

  togglePauseResume(job: any) {

    const now = new Date();
        const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const employeeCode = userDetails.employeeCode || '';

const localDateTime =
  now.getFullYear() + '-' +
  String(now.getMonth() + 1).padStart(2, '0') + '-' +
  String(now.getDate()).padStart(2, '0') + 'T' +
  String(now.getHours()).padStart(2, '0') + ':' +
  String(now.getMinutes()).padStart(2, '0') + ':' +
  String(now.getSeconds()).padStart(2, '0');

const payload = {
  JobNumber: job.jobNumber,
  SerialNo: job.serialNo,
  OperationNumber: job.operationNumber,
  Wc: job.wcCode,
  Item: job.item,
  EmpNum: employeeCode,
  loginuser: employeeCode,
  startTime: localDateTime   // ✅ ADD THIS
};



    this.loader.show();
    if (job.isPaused) {
      // Resume
      this.jobService.startQCJob(payload).pipe( finalize(() => { this.loader.hide(); })).subscribe({
        next: () => {
          job.isPaused = false;

          job.timerInterval = this.ngZone.run(() => { // <-- Use NgZone
              return setInterval(() => {
                  job.elapsedSeconds++;
                  job.elapsedTime = this.formatElapsedTime(job.elapsedSeconds);
              }, 1000);
          });
          this.jobTimers[job.uniqueRowId] = job.timerInterval;

          Swal.fire('Resumed', `Job ${job.jobNumber} resumed successfully.`, 'success');
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to resume job', 'error')
      });
    } else {
      // Pause

      this.jobService.pauseSingleQCJob(payload).pipe( finalize(() => { this.loader.hide(); })).subscribe({
        next: () => {
          job.isPaused = true;

          if (job.timerInterval) {
            clearInterval(job.timerInterval);
            delete this.jobTimers[job.uniqueRowId];
          }

          Swal.fire('Paused', `Job ${job.jobNumber} paused successfully.`, 'success');
        },
        error: (err) => Swal.fire('Error', err.error?.message || 'Failed to pause job', 'error')
      });
    }
  }

 /** Complete Job */
completeQCJob(job: any) {
  const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
  const employeeCode = userDetails.employeeCode || '';
  const now = new Date();

  // ✅ Local datetime string (NO UTC)
  const localDateTime =
    now.getFullYear() + '-' +
    String(now.getMonth() + 1).padStart(2, '0') + '-' +
    String(now.getDate()).padStart(2, '0') + 'T' +
    String(now.getHours()).padStart(2, '0') + ':' +
    String(now.getMinutes()).padStart(2, '0') + ':' +
    String(now.getSeconds()).padStart(2, '0');

  const payload = {
    JobNumber: job.jobNumber,
    SerialNo: job.serialNo,
    OperationNumber: job.operationNumber,
    Wc: job.wcCode,
    Item: job.item,
    EmpNum: employeeCode,
    loginuser: employeeCode,
     startTime: localDateTime  
  };

  Swal.fire({
    title: 'Complete Job?',
    text: `Complete Job ${job.jobNumber} (Serial ${job.serialNo})?`,
    icon: 'question',
    showCancelButton: true,
    confirmButtonText: 'Yes, Complete'
  }).then(result => {
    if (result.isConfirmed) {
      this.loader.show();
      this.jobService.completeSingleQCJob(payload).pipe( finalize(() => { this.loader.hide(); })).subscribe({
        next: () => {
          Swal.fire('Completed!', `Job ${job.jobNumber} completed successfully.`, 'success');
          this.loadJobs();
          this.loadOngoingJobs();
        //  this.loadCompletedJobs();
        },
        error: (err) => {
          Swal.fire('Error', err.error?.message || 'Failed to complete job', 'error');
        }
      });
    }
  });
}

  // View report
 viewReport(job: any) {
  if (!job) return;

  this.loader.show();

  this.jobService.GetJobReport(job.jobNumber)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (data: any) => {
        if (!data) return;

        // Map jobData
        const jobData = {
          job: data.job,
          jobDate: data.jobDate ? new Date(data.jobDate).toLocaleDateString() : '---',
          preparedBy: data.preparedBy || '---',
          item: data.item || '---',
          description: data.itemDescription || '---'
        };

        // Map operations and items with types
        const jobOperations: {
          operationNo: string;
          description: string;
          items: { seq: number; item: string; description: string; qty: number }[];
        }[] = (data.operations || []).map((op: any) => ({
          operationNo: op.operNum.toString(),
          description: op.operationDescription || '---',
          items: (op.items || []).map((it: any) => ({
            seq: it.sequence,
            item: it.item,
            description: `${it.itemDescription} ${it.ufItemDescription2 || ''}`.trim(),
            qty: it.requiredQty
          }))
        }));

        // Create temporary HTML for PDF
        const wrapper = document.createElement('div');
        wrapper.classList.add('report-wrapper');

        let htmlContent = `
          <h2>Job Report: ${jobData.job}</h2>
          <p>Date: ${jobData.jobDate}</p>
          <p>Prepared By: ${jobData.preparedBy}</p>
          <p>Item: ${jobData.item}</p>
          <p>Description: ${jobData.description}</p>
        `;

        jobOperations.forEach((op: { operationNo: string; description: string; items: any[] }) => {
          htmlContent += `<h3>Operation: ${op.operationNo} - ${op.description}</h3>`;
          htmlContent += `<table border="1" cellspacing="0" cellpadding="5">
            <tr><th>Seq</th><th>Item</th><th>Description</th><th>Qty</th></tr>`;
          op.items.forEach((it: { seq: number; item: string; description: string; qty: number }) => {
            htmlContent += `<tr>
              <td>${it.seq}</td>
              <td>${it.item}</td>
              <td>${it.description}</td>
              <td>${it.qty}</td>
            </tr>`;
          });
          htmlContent += `</table><br/>`;
        });

        wrapper.innerHTML = htmlContent;
        document.body.appendChild(wrapper);

        // Generate PDF
        html2pdf().set({
          filename: `${jobData.job}_Report.pdf`,
          html2canvas: { scale: 2 },
          jsPDF: { unit: 'pt', format: 'a4', orientation: 'portrait' }
        }).from(wrapper).save().then(() => {
          document.body.removeChild(wrapper);
        });
      },
      error: (err) => console.error('Failed to fetch job report', err)
    });
}

  
 
  loadCompletedJobs() {
    this.isLoading = true;
    this.loader.show();

    this.jobService.GetCompletedQCJobs()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any) => {
          let jobs = res.data ?? [];

          const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
          const employeeCode = (userDetails.employeeCode || '').trim();
          const roleid = Number(userDetails.roleID);
          
          // Apply filter for NON-ADMIN roles
          if (roleid !== 1) {
            jobs = jobs.filter((job: any) =>
              (job.empNum || '').trim() === employeeCode.trim()
            );
          }

          

          //Group by Job/Operation/Serial only (ignore wcCode & qcgroup)
          const grouped: { [key: string]: any[] } = jobs.reduce((acc: { [key: string]: any[] }, row: any) => {
            const key = `${row.jobNumber}|${row.operationNumber}|${row.serialNo}|${row.qcgroup}`;
            if (!acc[key]) acc[key] = [];
            acc[key].push(row);
            return acc;
          }, {});

          // Build final array
          this.completedJobs = Object.values(grouped).map((group: any[]) => {
            // Sort by endTime descending to get latest completed row
            group.sort((a, b) => new Date(b.endTime || 0).getTime() - new Date(a.endTime || 0).getTime());
            const lastRow = group[0];

            return {
              ...lastRow,
              compositeKey: `${lastRow.jobNumber}-${lastRow.serialNo}-${lastRow.operationNumber}-${lastRow.qcgroup}`,
              allLogs: group
            };
          });

          this.totalRecords = this.completedJobs.length;
          this.isLoading = false;
        },
        error: (err) => {
          console.error('Error fetching completed jobs:', err);
          this.isLoading = false;
        }
      });
  }

loadScrappedJobs() {
  this.isLoading = true;
  this.loader.show();

  this.jobService.getJobAsScrapped()
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res: any) => {
        let jobs = res.data ?? [];

        // Optional: Role-based filtering
        const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
        const employeeCode = userDetails.employeeCode || '';
        const roleid = userDetails.roleID || '';
        if (roleid !== 1) {
          jobs = jobs.filter((job: any) =>
            (job.empNum || '').trim() === employeeCode.trim()
          );
        }


        // Group by Job/Operation/Serial only (ignore wcCode & qcgroup)
        const grouped: { [key: string]: any[] } = jobs.reduce((acc: { [key: string]: any[] }, row: any) => {
          const key = `${row.jobNumber}|${row.operationNumber}|${row.serialNo}|${row.qcgroup}`;
          if (!acc[key]) acc[key] = [];
          acc[key].push(row);
          return acc;
        }, {});

        // Build final array
        this.scrappedJobs = Object.values(grouped).map((group: any[]) => {
          // Sort by time (if applicable) or keep as-is
          group.sort((a, b) => new Date(b.endTime || 0).getTime() - new Date(a.endTime || 0).getTime());
          const lastRow = group[0];

          return {
            ...lastRow,
            compositeKey: `${lastRow.jobNumber}-${lastRow.serialNo}-${lastRow.operationNumber}-${lastRow.qcgroup}`,
            allLogs: group
          };
        });

        this.totalScrappedRecords = this.scrappedJobs.length;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error fetching scrapped jobs:', err);
        this.isLoading = false;
      }
    });
}



saveQCRemark(job: any) {

  console.log('Job full object:', job);
  console.log('Triggered saveQCRemark for job:', job);

  if (!job.remark || job.remark.trim() === '') {
    console.warn('Remark is empty. Nothing to save.');
    return;
  }

  const payload = {
    trans_num: job.trans_num,   
    Remark: job.remark
  };

  console.log('Saving remark payload:', payload);

  this.jobService.updateQCRemark(payload).subscribe({
    next: (res: any) => {
      Swal.fire({
        icon: 'success',
        title: 'Remark Updated',
        text: res.message || 'Remark updated successfully',
        timer: 2000,
        showConfirmButton: false
      });
       job.isRemarkSaved = true;
    },
    error: (err: any) => {
      console.error('Failed to update remark:', err);
      Swal.fire({
        icon: 'error',
        title: 'Error',
        text: err.error?.message || 'Failed to update remark'
      });
    }
  });
}


markAsScrapped(job: any) {
  console.log('Triggered markAsScrapped for job:', job);

  if (!job) {
    console.warn('No job object found.');
    return;
  }
 const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
  const employeeCode = userDetails.employeeCode || '';
  // Prepare payload for API
  const payload = {
    jobNumber: job.jobNumber,
    serialNo: job.serialNo,
    operationNumber: job.operationNumber,
    loginUser: employeeCode 
  };

  console.log('Scrap job payload:', payload);

  // Call the service
  this.jobService.markJobAsScrapped(payload).subscribe({
    next: (res: any) => {
      console.log('Scrap response:', res);

      if (res.success) {
        Swal.fire({
          icon: 'success',
          title: 'Job Scrapped',
          text: res.message || 'Job marked as scrapped successfully.',
          timer: 2000,
          showConfirmButton: false
        });

        // Update frontend state
        job.isScrapped = true;
        this.scrappedJobs.push(job);

        // Remove from completed jobs
        this.completedJobs = this.completedJobs.filter(
          j => j.serialNo !== job.serialNo
        );
      } else {
        Swal.fire({
          icon: 'warning',
          title: 'Not Updated',
          text: res.message || 'Job could not be marked as scrapped.'
        });
      }
    },
    error: (err: any) => {
      console.error('Failed to mark job as scrapped:', err);
      Swal.fire({
        icon: 'error',
        title: 'Error',
        text: err.error?.message || 'Failed to mark job as scrapped.'
      });
    }
  });
}





}
