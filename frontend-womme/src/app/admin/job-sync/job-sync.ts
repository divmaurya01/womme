import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild } from '@angular/core';
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
  isSidebarHidden = false;
  isLoading = false;
  syncMessage: string | null = null;
  isError = false;


  
  transactions: any[] = [];
  totalRecords: number = 0;
  page: number = 0;
  size: number = 50;
  searchTerm: string = '';

  @ViewChild('dt') dt!: Table;
  dtTrigger: Subject<any> = new Subject();

  constructor(private jobService: JobService, private router: Router, private route: ActivatedRoute,private loader:LoaderService) {}

  ngAfterViewInit(): void {
    this.dtTrigger.next(true);
  }

  ngOnInit(): void {
    this.loadJobsLazy();
  }

  loadJobsLazy(event?: any) {
    this.isLoading = true;
    const page = event?.first ? event.first / event?.rows : 0;
    const size = event?.rows || 50;
    this.loader.show();
    this.jobService.GetJobs(page, size, this.searchTerm)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res: any) => {
          this.transactions = (res.data ?? []).map((x: any) => ({
          jobNumber: x.job.trim(),
          qtyReleased: x.quantity,
          item: x.item.trim(),
        
          operationNumber: x.operNo,
          wcCode: x.wcCode.trim(),
          
        }));
        this.totalRecords = res.totalRecords ?? 0;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  onSearchChange(value: string) {
    this.searchTerm = value;
    this.loadJobsLazy({ first: 0, rows: this.size });
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  ngOnDestroy(): void {
    this.dtTrigger.unsubscribe();
  }

  downloadQr(job: string) {
    this.loader.show();
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






}
