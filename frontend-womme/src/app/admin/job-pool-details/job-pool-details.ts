import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { JobService } from '../../services/job.service';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { ZXingScannerModule } from '@zxing/ngx-scanner';
import { Table } from 'primeng/table';
import { finalize } from 'rxjs/operators';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { BrowserQRCodeReader } from '@zxing/browser';
import Swal from 'sweetalert2';


@Component({
  selector: 'app-job-pool-details',
  templateUrl: './job-pool-details.html',
  styleUrls: ['./job-pool-details.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HeaderComponent,
    SidenavComponent,
    TableModule,
    DialogModule,
    ZXingScannerModule
  ]
})

export class JobPoolDetails implements OnInit {
  jobs: any[] = [];
  poolStatus: 'idle' | 'running' | 'paused' | 'completed' = 'idle';
  globalTimer: string = '00:00:00';
  isSidebarHidden = false;
  poolNumber:any;
  private timerInterval: any;
  private poolStartTime: Date | null = null; // first running start
  private accumulatedTime = 0; // total time in ms
  allJobsRaw: any;

  constructor(private jobService: JobService, private route: ActivatedRoute) {}

  ngOnInit(): void {
  // Read query param
    this.route.queryParams.subscribe(params => {
      this.poolNumber = params['jobPoolNumber'];
      if (this.poolNumber) {
        this.loadJobs(this.poolNumber);
      } else {
        console.warn('No jobPoolNumber provided in query params.');
      }
    });
  }

  ngOnDestroy(): void {
    if (this.timerInterval) clearInterval(this.timerInterval);
  }

  toggleSidebar() {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  loadJobs(poolNumber: string): void {
    this.jobService.JobPoolDetails(poolNumber).subscribe({
      next: (res: any) => {
        // Store raw response for timer calculations
        this.allJobsRaw = res.data; // <-- first response directly

        // Now create a filtered array for display (deduplicated)
        const latestJobsMap = new Map<string, any>();
        this.allJobsRaw.forEach((job: { job: any; operation: any; employee: any; workCenter: any; status_Time: string | number | Date; }) => {
          const key = `${job.job}|${job.operation}|${job.employee}|${job.workCenter}`;
          const existing = latestJobsMap.get(key);

          // Keep only latest per key
          if (!existing || new Date(job.status_Time) > new Date(existing.status_Time)) {
            latestJobsMap.set(key, job);
          }
        });

        // Display array (deduplicated)
        // Expand jobs by quantity
        this.jobs = [];
        Array.from(latestJobsMap.values()).forEach((job: any) => {
          for (let i = 1; i <= job.qty; i++) {
            this.jobs.push({
              ...job,
              qty: 1,
              serialNo: `${job.job}-${i}`
            });
          }
        });

        // Call your timer calculation using the raw array
        this.calculatePoolTimer(this.allJobsRaw);

      },
      error: err => {
        console.error('Error loading jobs', err);
        Swal.fire('Error', 'Could not load job pool details', 'error');
      }
    });
  }


  private calculatePoolTimer(rawJobs: any[] = this.allJobsRaw) {
    if (!rawJobs || rawJobs.length === 0) {
      this.globalTimer = '00:00:00';
      this.poolStatus = 'idle';
      return;
    }

    // Sort jobs by status_Time ascending
    const sortedJobs = [...rawJobs].sort(
      (a, b) => new Date(a.status_Time).getTime() - new Date(b.status_Time).getTime()
    );

    this.accumulatedTime = 0;
    this.poolStartTime = null;

    for (let i = 0; i < sortedJobs.length; i++) {
      const job = sortedJobs[i];
      const nextJob = sortedJobs[i + 1];

      const startTime = new Date(job.status_Time).getTime();
      const endTime = nextJob ? new Date(nextJob.status_Time).getTime() : Date.now();

      if (job.status_ID === 1 || job.status_ID === 2) {
        this.accumulatedTime += endTime - startTime;
      }
    }

    // Determine last status for pool
    const lastJob = sortedJobs[sortedJobs.length - 1];
    switch (lastJob.status_ID) {
      case 1:
        this.poolStatus = 'running';
        this.poolStartTime = new Date(lastJob.status_Time);
        break;
      case 2:
        this.poolStatus = 'paused';
        this.poolStartTime = null;
        break;
      case 3:
        this.poolStatus = 'completed';
        this.poolStartTime = null;
        break;
      default:
        this.poolStatus = 'idle';
        this.poolStartTime = null;
        break;
    }

    this.updateGlobalTimer();
    if (this.poolStatus === 'running') this.startLiveTimer();
  }


  private startLiveTimer() {
    if (this.timerInterval) clearInterval(this.timerInterval);

    this.timerInterval = setInterval(() => {
      const totalMs = this.accumulatedTime + (this.poolStartTime ? Date.now() - this.poolStartTime.getTime() : 0);
      this.globalTimer = this.formatTime(totalMs);
    }, 1000);
  }

  private updateGlobalTimer() {
    const totalMs = this.accumulatedTime + (this.poolStatus === 'running' && this.poolStartTime ? Date.now() - this.poolStartTime.getTime() : 0);
    this.globalTimer = this.formatTime(totalMs);
  }

  private formatTime(ms: number): string {
    const totalSeconds = Math.floor(ms / 1000);
    const hours = Math.floor(totalSeconds / 3600).toString().padStart(2, '0');
    const minutes = Math.floor((totalSeconds % 3600) / 60).toString().padStart(2, '0');
    const seconds = (totalSeconds % 60).toString().padStart(2, '0');
    return `${hours}:${minutes}:${seconds}`;
  }

  
 holdAll() {
  if (!this.poolNumber) return;

  this.jobService.jobPoolHold(this.poolNumber).subscribe({
    next: (res: any) => {
      Swal.fire('Success', 'Job pool put on hold', 'success');

      // Add elapsed time to accumulatedTime
      if (this.poolStartTime) {
        this.accumulatedTime += Date.now() - this.poolStartTime.getTime();
      }

      // Stop live timer
      if (this.timerInterval) clearInterval(this.timerInterval);
      this.timerInterval = null;

      // Update status locally
      this.poolStatus = 'paused';
      this.poolStartTime = null; // reset start time
      this.updateGlobalTimer();

      // Reload jobs to reflect changes
      this.loadJobs(this.poolNumber);
    },
    error: err => {
      console.error('Error holding job pool', err);
      Swal.fire('Error', 'Could not put job pool on hold', 'error');
    }
  });
}


  completeAll() {
    if (!this.poolNumber) return;

    this.jobService.jobPoolComplete(this.poolNumber).subscribe({
      next: (res: any) => {
        Swal.fire('Success', 'Job pool completed', 'success');

        // Stop live timer
        if (this.timerInterval) clearInterval(this.timerInterval);
        this.timerInterval = null;

        // Update status locally
        this.poolStatus = 'completed';
        this.updateGlobalTimer();

        // Reload jobs to reflect changes
        this.loadJobs(this.poolNumber);
      },
      error: err => {
        console.error('Error completing job pool', err);
        Swal.fire('Error', 'Could not complete job pool', 'error');
      }
    });
  }


  startAll() {
    if (!this.poolNumber) return;

    this.jobService.jobPoolresume(this.poolNumber).subscribe({
      next: (res: any) => {
        Swal.fire('Success', 'Job pool started', 'success');

        // Only stop old interval
        if (this.timerInterval) clearInterval(this.timerInterval);

        // Set pool status to running
        this.poolStatus = 'running';

        // If timer was previously running and paused, just update poolStartTime to now
        // accumulatedTime remains as before
        this.poolStartTime = new Date();

        // Start live timer
        this.startLiveTimer();

        // Reload jobs if needed
        this.loadJobs(this.poolNumber);
      },
      error: err => {
        console.error('Error starting job pool', err);
        Swal.fire('Error', 'Could not start job pool', 'error');
      }
    });
  }



  startJob(job: any) {
    console.log("Start clicked for", job.serialNo);
    // TODO: call service for start
  }

  holdJob(job: any) {
    console.log("Hold clicked for", job.serialNo);
    // TODO: call service for hold
  }

  completeJob(job: any) {
    console.log("Complete clicked for", job.serialNo);
    // TODO: call service for complete
  }


  scrapAll() { console.log('Scrap all jobs'); }
  moveAll() { console.log('Move all jobs'); }
}

