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
  selector: 'app-job-details',
  templateUrl: './job-posted-details.html',
  styleUrls: ['./job-posted-details.scss'],
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
export class JobPostedDetailComponent implements OnInit {
  // layout
  isSidebarHidden = false;
  jobDetails: any[] = [];
  loading = false;

  job: any;
  operation: any;
  releasedQty: any;
  machineNumber:any;
  workCenter:any;
  emp_num:any;
  selectedTransNum: string | null = null;
  selectedSerialNo: string | null = null;
  selectedRow: any = null;
  scannedSummary = {
    job: null,
    operation: null,
    machine: null,
    employee: null
  };

  elapsedTimes: { [key: string]: string } = {};
  timerIntervals: { [key: string]: any } = {};

  showWizard = false;
  wizardStep = 1;               
  scannedData: any = null;
  stepValid = false;
  useCamera = false;            
  availableDevices: MediaDeviceInfo[] = [];
  selectedDevice?: MediaDeviceInfo;

  qrReader = new BrowserQRCodeReader();
  

  @ViewChild('dt') dt!: Table;

  constructor(
    private jobService: JobService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const jobId = this.route.snapshot.queryParamMap.get('jb_id') || '';
    const operNum = this.route.snapshot.queryParamMap.get('oper_num') || '0';
    const trans_num = this.route.snapshot.queryParamMap.get('trans_num') || '0';
    if (jobId && operNum && trans_num) this.getJobDetails(jobId, operNum, trans_num);
  }

 

   getJobDetails(jobId: string, operNum: string, trans_num: string) {
    this.loading = true;

    this.jobService
      .GetJobPostedTransFullDetails(jobId, operNum, trans_num)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (res: any) => {
          if (!res || !res.employeeLogs || res.employeeLogs.length === 0) {
            this.jobDetails = [];
            this.job = '';
            this.operation = '';
            this.releasedQty = 0;
            return;
          }

          // Assign top-level job info
          this.job = res.job;
          this.operation = res.operationNumber;
          this.selectedTransNum = res.transactionNumber;
          this.selectedSerialNo = ''; // optionally set default serial
          this.selectedRow = null; // reset selection
          this.workCenter = res.workCenter;

          // Assign all employee logs
          this.jobDetails = res.employeeLogs;

          // Optional: select the first serialNo by default
          if (this.jobDetails.length > 0) {
            this.selectedSerialNo = this.jobDetails[0].serialNo;
            this.selectedRow = this.jobDetails[0].logEntries;
          }
        },
        error: (err) => {
          console.error('Error while fetching job details:', err);
          this.jobDetails = [];
          this.job = '';
          this.operation = '';
          this.releasedQty = 0;
        }
      });
  }

  // Calculate total hours for a log
getTotalHours(log: any): string {
  if (!log.logEntries || log.logEntries.length === 0) return '00:00:00';

  let totalMs = 0;

  // Loop over all log entries in pairs: start → pause/complete
  for (let i = 0; i < log.logEntries.length; i++) {
    const entry = log.logEntries[i];

    if (entry.statusID === 1) { // Started
      // Find next pause or complete entry
      const nextEntry = log.logEntries.slice(i + 1).find((e: { statusID: number; }) => e.statusID === 2 || e.statusID === 3);
      if (nextEntry) {
        const startTime = new Date(entry.statusTime).getTime();
        const endTime = new Date(nextEntry.statusTime).getTime();
        totalMs += endTime - startTime;
        i = log.logEntries.indexOf(nextEntry); // skip to nextEntry
      }
    }
  }

  // Convert milliseconds to hh:mm:ss
  const totalSeconds = Math.floor(totalMs / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${hours.toString().padStart(2,'0')}:${minutes.toString().padStart(2,'0')}:${seconds.toString().padStart(2,'0')}`;
}



    



  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }
}
