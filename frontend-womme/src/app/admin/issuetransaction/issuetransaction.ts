import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule, Table } from 'primeng/table';
import Swal from 'sweetalert2';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
import * as XLSX from 'xlsx';

@Component({
  selector: 'app-unposted-job-transaction',
  templateUrl: './issuetransaction.html',
  styleUrl: './issuetransaction.scss',
  standalone: true,
  imports: [
    CommonModule,
    HeaderComponent,
    SidenavComponent,
    TableModule,
    FormsModule
  ]
})
export class Issuetransaction implements OnInit {

  @ViewChild('dt') dt!: Table;

  employeeCode: string = '';
  role_id: number = 0;
  transactions: any[] = [];

  totalRecords: number = 0;
  page: number = 0;
  size: number = 200;
  searchTerm: string = '';
  isLoading: boolean = false;
  isSidebarHidden = false;
  
  filteredTransactions: any[] = [];
  globalSearch: string = '';
  


  constructor(
    private jobService: JobService,
    private loader: LoaderService
  ) {}

  ngOnInit(): void {
    this.loadJobs();
  }

  // ✅ Load only job data — nothing else
  loadJobs() {
  this.isLoading = true;
  this.loader.show();

  const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
  this.employeeCode = userDetails?.employeeCode;

  this.jobService.GetIssuedTransactions(0, 100000, '', this.employeeCode)
    .pipe(finalize(() => {
      this.isLoading = false;
      this.loader.hide();
    }))
    .subscribe({
      next: (res: any) => {
        this.transactions = (res.data ?? []).map((x: any) => ({
          serialNo: x.serialNo?.trim(),
          jobNumber: x.job?.trim(),
          qtyReleased: x.qtyReleased,
          item: x.item?.trim(),
          operationNumber: x.operNum,
          wcCode: x.wcCode?.trim()
        }));

        this.filteredTransactions = [...this.transactions];
      }
    });
}



  startIssueJob(job: any) {
    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    const employeeCode = userDetails.employeeCode || '';

    const payload = {
      JobNumber: job.jobNumber,
      SerialNo: job.serialNo,
      OperationNumber: job.operationNumber,
      Wc: job.wcCode,
      Item: job.item ?? null,
      QtyReleased: job.qtyReleased,
      EmpNum: employeeCode,
      loginuser: employeeCode
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

        this.jobService.startIssueJob(payload)
          .pipe(finalize(() => this.loader.hide()))
          .subscribe({
            next: (res: any) => {
              Swal.fire('Started!', res.message || 'Item Issued successfully', 'success');
              this.loadJobs();
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

  onGlobalSearch(): void {
    const raw = this.globalSearch.toLowerCase().trim();

    if (!raw) {
      this.filteredTransactions = [...this.transactions];
      return;
    }

    const keys = raw.split('|').map(k => k.trim());

    this.filteredTransactions = this.transactions.filter(job =>
      keys.some(keyword =>
        Object.values(job).some(val =>
          val?.toString().toLowerCase().includes(keyword)
        )
      )
    );
  }

  exportToExcel(): void {
    if (!this.filteredTransactions.length) return;

    const exportData = this.filteredTransactions.map((x, i) => ({
      'Sr No': i + 1,
      'Job': x.jobNumber,
      'Serial': x.serialNo,
      'Item': x.item,
      'Qty': x.qtyReleased,
      'Operation': x.operationNumber
    }));

    const ws = XLSX.utils.json_to_sheet(exportData);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Transactions');
    XLSX.writeFile(wb, 'IssuedTransactions.xlsx');
  }


  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }
}
