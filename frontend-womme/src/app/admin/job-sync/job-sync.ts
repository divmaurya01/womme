import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject } from 'rxjs';
import { Table } from 'primeng/table';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service'; // import the interface
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule } from 'primeng/table';
import Swal from 'sweetalert2';
import { ActivatedRoute, Router } from '@angular/router';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
import * as XLSX from 'xlsx';

@Component({
  selector: 'app-job-sync',
  templateUrl: './job-sync.html',
  styleUrls: ['./job-sync.scss'],
  standalone: true,
  imports: [
    CommonModule,
    HeaderComponent,
    SidenavComponent,
    TableModule,
    FormsModule
  ]
})
export class JobSyncComponent implements OnInit, AfterViewInit, OnDestroy {
 isSidebarHidden = window.innerWidth <= 1024;
  isLoading = false;
  syncMessage: string | null = null;
  isError = false;
  autoSyncEnabled: boolean = false;
  autoSyncInterval: any;
  jobs: any[] = [];
  filteredJobs: any[] = [];
  globalSearch: string = '';


  
  transactions: any[] = [];
  totalRecords: number = 0;
  page: number = 0;
  size: number = 200;
  searchTerm: string = '';

  @ViewChild('dt') dt!: Table;
  dtTrigger: Subject<any> = new Subject();

  constructor(private jobService: JobService, private router: Router, private route: ActivatedRoute,private loader:LoaderService) {}

  ngAfterViewInit(): void {
    this.dtTrigger.next(true);
  }

  ngOnInit(): void {
    this.checkScreenSize();
    this.loadJobs();
    const saved = localStorage.getItem('autoSync');
    this.autoSyncEnabled = saved === 'true';
    if (this.autoSyncEnabled) this.startAutoSync();
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
  loadJobs(): void {
  this.loader.show();

  this.jobService.GetJobs(0, 100000, '') // large size
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res: any) => {
        if (res && res.data) {
          this.jobs = res.data.map((x: any, index: number) => ({
            srNo: index + 1,
            jobNumber: x.job.trim(),
            qtyReleased: x.quantity,
            item: x.item.trim(),
            operationNumber: x.operNo,
            wcCode: x.wcCode.trim()
          }));

          this.filteredJobs = [...this.jobs];
        }
      }
    });
}


  onGlobalSearch(): void {
    const rawSearch = this.globalSearch.toLowerCase().trim();

    if (!rawSearch) {
      this.filteredJobs = [...this.jobs];
      return;
    }

    const keywords = rawSearch
      .split('|')
      .map(k => k.trim())
      .filter(k => k.length > 0);

    this.filteredJobs = this.jobs.filter(job =>
      keywords.some(keyword =>
        Object.values(job).some(value =>
          value?.toString().toLowerCase().includes(keyword)
        )
      )
    );
  }


  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  ngOnDestroy(): void {
    this.dtTrigger.unsubscribe();
    this.stopAutoSync();
  }

  downloadQr(job: string) {
    this.loader.show();
    if (!job?.trim()) {
      console.warn('Blocked QR download — job not ready');
      return;
    }
    
    this.jobService.GenerateQrWithJob(job)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe((blob: Blob) => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `Job-${job}-QR.png`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    }, error => {
      console.error("Error downloading QR:", error);
    });
  }

  syncJob() {
    this.isLoading = true;
    this.syncMessage = null;
    this.isError = false;
    this.loader.show();
    this.jobService.SyncJobMst()
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

  // 🔹 New Sync All Tables button
   syncAllTables() {
  this.isLoading = true;
  this.syncMessage = null;
  this.isError = false;
  this.loader.show();

  this.jobService.SyncAllTables()
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res: any) => {
        this.isLoading = false;
        this.syncMessage = res; // backend returns text
        this.isError = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.syncMessage = err.error || "Something went wrong while syncing all tables.";
        this.isError = true;
      }
    });
}

  onJobClick(job: string): void {
    const currentParams = this.route.snapshot.queryParams;
      const ss_id = currentParams['ss_id'] || '';

      this.router.navigate(['/job-sync-details'], {
        queryParams: {
          ss_id: ss_id,
          jb_id: job
        }
      });
  }


  onAutoSyncChange() {
    localStorage.setItem('autoSync', this.autoSyncEnabled.toString());
      if (this.autoSyncEnabled) {
        this.startAutoSync();
      } else {
        this.stopAutoSync();
      }
    }

    startAutoSync() {
      // Prevent duplicate intervals
      if (this.autoSyncInterval) {
        clearInterval(this.autoSyncInterval);
      }

      // Run immediately once
      this.syncAllTables();

      // Then every 5 minutes
      this.autoSyncInterval = setInterval(() => {
        this.syncAllTables();
      }, 5 * 60 * 1000); // 5 minutes
    }

    stopAutoSync() {
      if (this.autoSyncInterval) {
        clearInterval(this.autoSyncInterval);
        this.autoSyncInterval = null;
      }
    }


    exportJobsToExcel(): void {
    if (!this.filteredJobs.length) return;

    const exportData = this.filteredJobs.map((job, index) => ({
      'Sr No': index + 1,
      'Job': job.jobNumber,
      'Qty': job.qtyReleased,
      'Item': job.item,
      'Operation': job.operationNumber,
      'WC': job.wcCode
    }));

    const worksheet = XLSX.utils.json_to_sheet(exportData);
    const workbook = {
      Sheets: { Jobs: worksheet },
      SheetNames: ['Jobs']
    };

    XLSX.writeFile(workbook, 'Jobs.xlsx');
  }




}
