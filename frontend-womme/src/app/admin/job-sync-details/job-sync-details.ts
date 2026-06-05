import { Component, OnInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import Swal from 'sweetalert2';
import { ActivatedRoute } from '@angular/router';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-job-details',
  templateUrl: './job-sync-details.html',
  styleUrls: ['./job-sync-details.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HeaderComponent,
    SidenavComponent,
    TableModule,
    DialogModule
  ]
})
export class JobSyncDetailComponent implements OnInit {
  isSidebarHidden = window.innerWidth <= 1024;

  jobNumber: string = '';
  jobMeta: any = null;
  flatRows: any[] = [];
  private loggedUser: string = '';

  constructor(
    private jobService: JobService,
    private route: ActivatedRoute,
    private loader: LoaderService
  ) {}

  ngOnInit(): void {
    this.checkScreenSize();

    // Get logged user from localStorage — same pattern as your other components
    this.loggedUser = localStorage.getItem('emp_num') ?? 'SYSTEM';

    this.route.queryParamMap.subscribe(params => {
      this.jobNumber = params.get('jb_id') ?? '';
      if (this.jobNumber) {
        this.fetchJobProgressDetail(this.jobNumber);
      }
    });
  }

  @HostListener('window:resize')
  onResize() { this.checkScreenSize(); }

  checkScreenSize() {
    this.isSidebarHidden = window.innerWidth <= 1024;
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  fetchJobProgressDetail(job: string): void {
    this.loader.show();
    this.jobService.getJobProgressDetail(job)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any) => {
          this.jobMeta = {
            job:     res.job,
            suffix:  res.suffix,
            item:    res.item,
            qty:     res.qty,
            jobDate: res.jobDate,
            stat:    res.stat
          };

          this.flatRows = (res.data ?? []).map((row: any) => ({
            ...row,
            manualSerial: row.manualSerial ?? ''
          }));
        },
        error: (err) => {
          console.error(err);
          Swal.fire('Error', 'Failed to load job details', 'error');
        }
      });
  }

  onEnterSerial(row: any): void {
    const manualVal = row.manualSerial?.trim();
    if (!manualVal) {
      Swal.fire('Validation', 'Please enter a manual serial number.', 'warning');
      return;
    }

    const payload = {
      job:          this.jobNumber,
      serialIndex:  row.serialIndex,
      systemSerial: row.serialNo,
      manualSerial: manualVal,
      savedBy:      this.loggedUser
    };

    this.loader.show();
    this.jobService.saveManualSerial(payload)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          Swal.fire({
            icon: 'success',
            title: 'Saved',
            text: `"${manualVal}" saved for ${row.serialNo}`,
            timer: 1500,
            showConfirmButton: false
          });
        },
        error: (err) => {
          Swal.fire('Error', err?.error?.message ?? 'Failed to save.', 'error');
        }
      });
  }
}