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
    ZXingScannerModule
  ]
})
export class VerifyTransaction implements OnInit {

  @ViewChild('dt') dt!: Table;

  transactions: any[] = [];
  totalRecords = 0;
  isLoading = false;
  searchTerm = '';

  isSidebarHidden = false;

  constructor(
    private jobService: JobService,
    private loaderService: LoaderService,
    private router: Router,
    private route: ActivatedRoute,
    private zone: NgZone
  ) {}

  ngOnInit(): void {
    // initial load
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
          this.transactions = res.data || [];
          this.totalRecords = res.totalRecords || 0;
        },
        error: () => {
          Swal.fire('Error', 'Failed to load verify transactions', 'error');
        }
      });
  }

 
  onSearchChange(value: string) {
    this.searchTerm = value;
    this.dt?.reset(); // reload table
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
      Item: job.item,
      EmpNum: employeeCode,
      loginuser: employeeCode
    };
    

    this.jobService.verifyTransaction(payload)
      .pipe(finalize(() => this.loaderService.hide()))
      .subscribe({
        next: () => {
          Swal.fire('Success', 'Transaction verified successfully', 'success');
          this.dt.reset(); // reload table
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
          this.dt.reset();
        },
        error: () => {
          Swal.fire('Error', 'Submit failed', 'error');
        }
      });
  }
}
