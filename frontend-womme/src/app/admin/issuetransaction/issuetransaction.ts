import { Component, HostListener, OnInit, ViewChild } from '@angular/core';
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

  allTransactions: any[] = [];     // master list


  totalRecords: number = 0;
  page: number = 0;
  size: number = 200;
  searchTerm: string = '';
  isLoading: boolean = false;
 
   isSidebarHidden = window.innerWidth <= 1024;

  
  filteredTransactions: any[] = [];
  globalSearch: string = '';
  
  selectedJobs: any[] = [];

  constructor(
    private jobService: JobService,
    private loader: LoaderService
  ) {}

  ngOnInit(): void {
    this.checkScreenSize();
    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    this.employeeCode = userDetails?.employeeCode;
    this.role_id = userDetails?.roleID;

    this.loadJobs();
  }

 private empMatches(jobEmpNum: string, employeeCode: string): boolean {
  if (!jobEmpNum || !employeeCode) return false;

  return jobEmpNum
    .split(',')
    .map(e => e.trim())
    .includes(employeeCode);
}



  loadJobs() {
    this.isLoading = true;
    this.loader.show();

    this.jobService
      .GetIssuedTransactions(0, 100000, '', this.employeeCode)
      .pipe(finalize(() => {
        this.isLoading = false;
        this.loader.hide();
      }))
      .subscribe({
        next: (jobRes: any) => {
          const rawJobs = jobRes.data ?? [];

          // 🔹 Load next-operation info (SAME as unposted)
          this.jobService.getIsNextJobActive().subscribe({
            next: (nextRes: any) => {

              // --------------------------------------
              // Build job|serial → next operations map
              // --------------------------------------
              const nextOpMap = new Map<string, number[]>();

              (nextRes.data ?? []).forEach((x: any) => {
                const key = `${x.job}|${x.serialNo}`;
                if (!nextOpMap.has(key)) nextOpMap.set(key, []);
                nextOpMap.get(key)!.push(Number(x.nextOper));
              });

              // --------------------------------------
              // Normalize jobs
              // --------------------------------------
              let jobs = rawJobs.map((x: any) => ({
                id: `${x.job?.trim()}_${x.serialNo?.trim()}_${x.operNum}_${x.wcCode?.trim()}`,
                serialNo: x.serialNo?.trim(),
                jobNumber: x.job?.trim(),
                qtyReleased: x.qtyReleased,
                item: x.item?.trim(),
                operationNumber: x.operNum,
                wcCode: x.wcCode?.trim(),
                emp_num: x.empNum?.trim() ?? '' // IMPORTANT
              }));

              // --------------------------------------
              // ROLE-BASED FILTERING
              // --------------------------------------
              if (this.role_id === 2) {
              jobs = jobs.filter((job: { emp_num: string; }) =>
                this.empMatches(job.emp_num, this.employeeCode)
              );
            }


              // --------------------------------------
              // FINAL ASSIGNMENT (ONCE)
              // --------------------------------------
              this.allTransactions = jobs;
              this.filteredTransactions = [...jobs];
              this.transactions = this.filteredTransactions;
              this.totalRecords = jobs.length;

              console.log('Final ISSUE jobs shown:', jobs.length);
            }
          });
        },
        error: err => {
          console.error(err);
          this.loader.hide();
          this.isLoading = false;
        }
      });
  }



private buildStartPayload(job: any) {
  const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
  const employeeCode = userDetails.employeeCode || '';

  const now = new Date();
  const localDateTime =
    now.getFullYear() + '-' +
    String(now.getMonth() + 1).padStart(2, '0') + '-' +
    String(now.getDate()).padStart(2, '0') + 'T' +
    String(now.getHours()).padStart(2, '0') + ':' +
    String(now.getMinutes()).padStart(2, '0') + ':' +
    String(now.getSeconds()).padStart(2, '0');

  return {
    JobNumber: job.jobNumber,
    SerialNo: job.serialNo,
    OperationNumber: job.operationNumber,
    Wc: job.wcCode,
    Item: job.item ?? null,
    QtyReleased: job.qtyReleased,
    EmpNum: employeeCode,
    loginuser: employeeCode,
    StartTime: localDateTime,      // ✅ add start time
    EndTime: localDateTime         // ✅ optional, can be same as start
  };
}




  startIssueJob(job: any) {
    
    const payload = this.buildStartPayload(job);

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
      this.filteredTransactions = [...this.allTransactions];
      return;
    }

    const keys = raw.split('|').map(k => k.trim());

    this.filteredTransactions = this.allTransactions.filter(job =>
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


  async startSelectedJobs() {
    if (!this.selectedJobs.length) return;

    this.isLoading = true;
    

    let successCount = 0;
    let failedJobs: string[] = [];

    for (const job of this.selectedJobs) {
      const payload = this.buildStartPayload(job);

      try {
        await this.jobService.startIssueJob(payload).toPromise();
        successCount++;
      } catch (err) {
        console.error('Failed job:', job.jobNumber, err);
        failedJobs.push(`${job.jobNumber} (${job.serialNo})`);
      }
    }

    this.loader.hide();
    this.isLoading = false;
    this.selectedJobs = [];

    this.loadJobs();

    // ✅ FINAL RESULT POPUP
    if (failedJobs.length === 0) {
      Swal.fire({
        icon: 'success',
        title: 'Jobs Started',
        text: `${successCount} job(s) started successfully.`,
      });
    } else {
      Swal.fire({
        icon: 'warning',
        title: 'Partial Success',
        html: `
          <p><b>${successCount}</b> job(s) started successfully.</p>
          <p><b>${failedJobs.length}</b> job(s) failed:</p>
          <small>${failedJobs.join('<br>')}</small>
        `,
      });
    }
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


  


}
