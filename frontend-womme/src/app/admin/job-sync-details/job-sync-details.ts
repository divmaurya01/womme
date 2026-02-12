import { Component, OnInit, ViewChild, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule, Table } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import Swal from 'sweetalert2';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-job-details',
  templateUrl: './job-sync-details.html',
  styleUrls: ['./job-sync-details.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, HeaderComponent, SidenavComponent, TableModule, DialogModule]
})
export class JobSyncDetailComponent implements OnInit {
 isSidebarHidden = window.innerWidth <= 1024;
  jobStatusByEmployee: any[] = [];
  rawJobData: any[] = [];
  jobNumber: string = '';
  jobInfo: any = null;
statusNames: any;

  constructor(private jobService: JobService, private route: ActivatedRoute,private loader:LoaderService) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.loader.show();
    this.route.queryParamMap
    .pipe(finalize(() => this.loader.hide()))
    .subscribe(params => {
      const ss_id = params.get('ss_id') ?? '';
      this.jobNumber = params.get('jb_id') ?? '';

      console.log('ss_id:', ss_id);
      console.log('jobNumber:', this.jobNumber);

      if (this.jobNumber) {
        this.fetchJobDetails(this.jobNumber);
      }
    });
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
  fetchJobDetails(job: string) {
    this.loader.show();
    this.jobService.getJobTrans(job)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (res: any) => {
        // assuming API returns array of JobTranMst objects
        if (res && res.length > 0) {
          this.jobInfo = res[0]; // just show first transaction
          this.rawJobData = res; // save full array if needed later
        } else {
          this.jobInfo = null;
        }
      },
      error: (err) => {
        console.error('Failed to fetch job details:', err);
        this.jobInfo = null;
        Swal.fire('Error', 'Failed to load job details', 'error');
      }
    });
  }



  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  

  
  



}
