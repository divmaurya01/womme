import { Component, OnInit, ViewChild, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule, Table } from 'primeng/table';
import { ZXingScannerModule } from '@zxing/ngx-scanner';
import { DialogModule } from 'primeng/dialog';
import Swal from 'sweetalert2';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { LoaderService } from '../../services/loader.service';
import { finalize, flatMap } from 'rxjs/operators';
import { BrowserQRCodeReader } from '@zxing/browser';
import { TabViewModule } from 'primeng/tabview';
import * as XLSX from 'xlsx';


@Component({
  selector: 'app-verify-transaction',
  templateUrl: './verify-transaction.html',
  styleUrls: ['./verify-transaction.scss'],
  standalone: true,
  imports: [
    CommonModule,
    HeaderComponent,
    SidenavComponent,
    TableModule,
    FormsModule,
    DialogModule,
    ZXingScannerModule,
    TabViewModule
  ]
})
export class VerifyTransaction implements OnInit {



  transactions: any[] = [];
  totalRecords = 0;
  isLoading = false;
  searchTerm = '';
  selectedTab: 'NEW' | 'ONGOING' | 'COMPLETED' = 'NEW';
  newJobs: any[] = [];
  ongoingJobs: any[] = [];
  completedJobs: any[] = [];
  activeTabIndex = 0;
  totalRecordsNew = 0;
  totalRecordsOngoing = 0;
  totalRecordsCompleted = 0;
  globalSearchNew: string = '';
  globalSearchOngoing: string = '';
  globalSearchCompleted: string = '';

  allNewJobs: any[] = [];
  allOngoingJobs: any[] = [];
  allCompletedJobs: any[] = [];




  isSidebarHidden = false;

  constructor(
    private jobService: JobService,
    private loaderService: LoaderService,
    private router: Router,
    private route: ActivatedRoute,
    private zone: NgZone
  ) { }

  ngOnInit(): void {
    this.loadJobs({
      first: 0,
      rows: 50
    });
    this.loadCompletedJobs();
  }


  toggleSidebar() {
    this.isSidebarHidden = !this.isSidebarHidden;
  }


  loadJobs(event: any) {
    const page = event.first / event.rows;
    const size = event.rows;

    this.isLoading = true;
    this.loaderService.show();

    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const employeeCode = userDetails.employeeCode || '';

    this.jobService
      .getVerifyTransactions(page, size, this.searchTerm, employeeCode)
      .pipe(finalize(() => {
        this.isLoading = false;
        this.loaderService.hide();
      }))
      .subscribe({
        next: (res: any) => {
          const data = res.data || [];

          data.forEach((x: any) => {
            x.isRemarkSaved = !!x.remark;   // if backend sends remark later
            x.verify = false;
          });

          this.newJobs = data.filter((x: { status: string; isActive: boolean; }) =>
              (!x.status || x.status === '') && x.isActive === false
            );

            this.ongoingJobs = data.filter((x: { status: string; isActive: boolean; }) =>
              x.status === '1' && x.isActive === true
            );

            this.allNewJobs = [...this.newJobs];
            this.allOngoingJobs = [...this.ongoingJobs];

            this.totalRecordsNew = this.newJobs.length;
            this.totalRecordsOngoing = this.ongoingJobs.length;


          
        },
        error: () => {
          Swal.fire('Error', 'Failed to load transactions', 'error');
        }
      });
  }



  filterByTab(data: any[]) {
    switch (this.selectedTab) {

      case 'NEW':
        return data.filter(x =>
          (x.status === null || x.status === '' || x.status === undefined) &&
          x.isActive === false
        );

      case 'ONGOING':
        return data.filter(x =>
          x.status === '1' &&
          x.isActive === true
        );

      case 'COMPLETED':
        return data.filter(x =>
          x.status === '3'
        );

      default:
        return data;
    }
  }

 /** Start Single Job */
startApproval(job: any) {
  const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
  const employeeCode = userDetails.employeeCode || '';

  const payload = {
    JobNumber: job.jobNumber || job.job,
    SerialNo: job.serialNo,
    OperationNumber: job.operationNumber || job.operNum,
    Wc: job.wcCode,
    Item: job.item,
    QtyReleased: job.qtyReleased,
    EmpNum: employeeCode,
    loginuser: employeeCode
  };

  Swal.fire({
    title: 'Start Job?',
    text: `Do you want to start Job ${payload.JobNumber} (Serial ${payload.SerialNo}) at WC ${payload.Wc}?`,
    icon: 'question',
    showCancelButton: true,
    confirmButtonText: 'Yes, Start',
    cancelButtonText: 'Cancel'
  }).then((result) => {
    if (result.isConfirmed) {
      this.loaderService.show();

      this.jobService.startJob(payload)
        .pipe(finalize(() => this.loaderService.hide()))
        .subscribe({
          next: (res: any) => {
            Swal.fire('Started!', `Job ${job.jobNumber} started successfully.`, 'success');
            location.reload();
          },
          error: (err) => {
            Swal.fire(
              'Error',
              err.error?.message || 'Failed to start job',
              'error'
            );
          }
        });
    }
  });
}


saveRemark(job: any) {

  if (!job.remark || job.remark.trim() === '') {
    Swal.fire('Warning', 'Please enter remark', 'warning');
    return;
  }

  const payload = {
    trans_num: job.trans_number,
    Remark: job.remark
  };

  this.loaderService.show();

  this.jobService.updateQCRemark(payload)
    .pipe(finalize(() => this.loaderService.hide()))
    .subscribe({
      next: (res: any) => {
        Swal.fire({
          icon: 'success',
          title: 'Remark Saved',
          timer: 1500,
          showConfirmButton: false
        });

        // ✅ UI STATE CHANGE
        job.isRemarkSaved = true;   // hide Save button
      },
      error: () => {
        Swal.fire('Error', 'Failed to save remark', 'error');
      }
    });
}



verifyJob(job: any) {

  const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
  const employeeCode = userDetails.employeeCode || '';

  const payload = {
    jobNumber: job.job,
    serialNo: job.serialNo,
    OperationNumber: job.operNum,
    wc: job.wcCode,    
    qtyReleased: job.qtyReleased,    
    empNum: employeeCode,
    loginuser: employeeCode
  };

  Swal.fire({
    text: `Do you want to start Job ${payload.jobNumber} (Serial ${payload.serialNo}) at WC ${payload.wc}?`,
    icon: 'question',
    showCancelButton: true,
    confirmButtonText: 'Verify'
  }).then(result => {
    if (!result.isConfirmed) return;

    this.loaderService.show();

    this.jobService.CompleteJob(payload)
      .pipe(finalize(() => this.loaderService.hide()))
      .subscribe({
        next: () => {
          Swal.fire('Verify', 'Job Verify successfully', 'success');
          job.verify = true;
          this.loadJobs({ first: 0, rows: 50 });
        },
        error: () => {
          Swal.fire('Error', 'Verify failed', 'error');
        }
      });
  });
}

loadCompletedJobs() {
  this.isLoading = true;
  this.loaderService.show();

  this.jobService
    .GetCompletedVerifyJob()
    .pipe(finalize(() => {
      this.isLoading = false;
      this.loaderService.hide();
    }))
    .subscribe({
      next: (res) => {
        this.completedJobs = res.data || [];
        this.allCompletedJobs = [...this.completedJobs];   
        this.totalRecordsCompleted = this.completedJobs.length;
      },

      error: () => {
        Swal.fire('Error', 'Failed to load completed jobs', 'error');
      }
    });
}




  onTabChange(event: any) {
    this.activeTabIndex = event.index;
  }


  onSearchChange(value: string) {
    this.searchTerm = value;
    this.loadJobs({ first: 0, rows: 50 });
  }



  verifyTransaction(job: any) {
    if (!job.remark || job.remark.trim() === '') {
      Swal.fire('Warning', 'Please enter remark before verify', 'warning');
      return;
    }

    Swal.fire({
      title: 'Verify Transaction?',
      text: 'Are you sure you want to verify this transaction?',
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Verify'
    }).then(result => {
      if (result.isConfirmed) {
        this.callVerifyApi(job);
      }
    });
  }

  private callVerifyApi(job: any) {
    this.loaderService.show();

    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const employeeCode = userDetails.employeeCode || '';

    const payload = {
      JobNumber: job.jobNumber,
      SerialNo: job.serialNo,
      OperationNumber: job.operationNumber,
      Wc: job.wcCode,
      qtyReleased:job.qtyReleased,
      Item: job.item,
      EmpNum: employeeCode,
      loginuser: employeeCode,
      Remark: job.remark
    };

    this.jobService.verifyTransaction(payload)
      .pipe(finalize(() => this.loaderService.hide()))
      .subscribe({
        next: () => {
          Swal.fire('Success', 'Transaction verified successfully', 'success');

          // Mark row as verified
          job.isVerified = true;
        },
        error: () => {
          Swal.fire('Error', 'Verification failed', 'error');
        }
      });
  }


  submitTransaction(job: any) {
    Swal.fire({
      title: 'Submit Transaction?',
      text: 'This action cannot be undone',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Submit'
    }).then(result => {
      if (result.isConfirmed) {
        this.callSubmitApi(job);
      }
    });
  }

  private callSubmitApi(job: any) {
    this.loaderService.show();

    this.jobService.submitTransaction(job)
      .pipe(finalize(() => this.loaderService.hide()))
      .subscribe({
        next: () => {
          Swal.fire('Submitted', 'Transaction submitted successfully', 'success');
        },
        error: () => {
          Swal.fire('Error', 'Submit failed', 'error');
        }
      });
  }

  onGlobalSearchNew() {
  const raw = this.globalSearchNew.toLowerCase().trim();

  if (!raw) {
    this.newJobs = [...this.allNewJobs];
    return;
  }

  const keywords = raw.split('|').map(k => k.trim());

  this.newJobs = this.allNewJobs.filter(job =>
    keywords.some(keyword =>
      Object.values(job).some(val =>
        val?.toString().toLowerCase().includes(keyword)
      )
    )
  );
}

onGlobalSearchOngoing() {
  const raw = this.globalSearchOngoing.toLowerCase().trim();

  if (!raw) {
    this.ongoingJobs = [...this.allOngoingJobs];
    return;
  }

  const keywords = raw.split('|').map(k => k.trim());

  this.ongoingJobs = this.allOngoingJobs.filter(job =>
    keywords.some(keyword =>
      Object.values(job).some(val =>
        val?.toString().toLowerCase().includes(keyword)
      )
    )
  );
}

onGlobalSearchCompleted() {
  const raw = this.globalSearchCompleted.toLowerCase().trim();

  if (!raw) {
    this.completedJobs = [...this.allCompletedJobs];
    return;
  }

  const keywords = raw.split('|').map(k => k.trim());

  this.completedJobs = this.allCompletedJobs.filter(job =>
    keywords.some(keyword =>
      Object.values(job).some(val =>
        val?.toString().toLowerCase().includes(keyword)
      )
    )
  );
}


exportExcel(data: any[], fileName: string) {
  if (!data.length) return;

  const ws = XLSX.utils.json_to_sheet(data);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Sheet1');
  XLSX.writeFile(wb, fileName + '.xlsx');
}





}
