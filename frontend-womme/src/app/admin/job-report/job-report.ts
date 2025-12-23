import { Component, OnInit } from '@angular/core';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-job-report',
  templateUrl: './job-report.html',
  styleUrls: ['./job-report.scss'],
  imports: [SidenavComponent, HeaderComponent, CommonModule]
})
export class JobReportComponent implements OnInit {

  isSidebarHidden = false;
  selectedJob: string | null = null;
  isLoading = true;

  jobProgressData: any[] = [];

  /** 🔹 LEVEL 1 JOBS */
  jobs: any[] = [];

  expandedJobs: { [job: string]: boolean } = {};
  expandedSerial: { [serial: string]: boolean } = {};

  constructor(private jobService: JobService) {}

  ngOnInit(): void {
    this.loadJobProgress();
  }

  
  loadJobProgress(): void {
    this.isLoading = true;

    this.jobService.getJobProgress().subscribe({
      next: (res: any[]) => {
        this.jobProgressData = res;

        this.jobs = res.map(j => ({
          job: j.jobNo,
          item: j.item,
          itemDesc: j.description,
          qty: j.qty,
          jobDate:j.jobDate,
          totalHrs: this.calculateJobTotalHours(j)
        }));

        this.isLoading = false;
      },
      error: err => {
        console.error('JobProgress API error', err);
        this.isLoading = false;
      }
    });
  }

  /** ================= LEVEL 1 ================= */
  toggleJob(jobNo: string) {
    this.selectedJob = jobNo;
    this.expandedJobs[jobNo] = !this.expandedJobs[jobNo];
  }

  /** ================= LEVEL 2 ================= */
  getSerials(job: any): string[] {
    const jobData = this.jobProgressData.find(j => j.jobNo === job.job);
    return jobData ? jobData.serials.map((s: any) => s.serialNo) : [];
  }

  toggleSerial(serial: string, event: MouseEvent) {
    event.stopPropagation();
    this.expandedSerial[serial] = !this.expandedSerial[serial];
  }

  getSerialStatus(serial: string): string {
    const s = this.findSerial(serial);
    if (!s) return 'No Transaction Yet';

    if (s.completedOperations === s.totalOperations) return 'Closed';
    if (s.operations.some((o: any) => o.status === 'Running')) return 'Running';
    if (s.operations.some((o: any) => o.status === 'Pause')) return 'Pause';
    return 'No Transaction Yet';
  }

  getRunningOperation(serial: string): string {
    const s = this.findSerial(serial);
    return s?.runningOperations ??  '-';
  }

  getCompletedOperation(serial: string): string {
    const s = this.findSerial(serial);
    return s?.completedOperations ?? '-';
  }
  getTotalOperation(serial:string): string{
  const s=this.findSerial(serial);
  return s?.totalOperations ?? '-';
  }
  getSerialStart(serial: string): string {
    const s = this.findSerial(serial);
    return s?.operations[0]?.startTime ?? '-';
  }

  getSerialEnd(serial: string): string {
    const s = this.findSerial(serial);
    const last = s?.operations[s.operations.length - 1];
    return last?.endTime ?? '-';
  }

  getSerialRunningHrs(serial: string): string {
    const s = this.findSerial(serial);
    return this.decimalHoursToHHMMSS(s?.totalHours ?? 0);
  }



  /** ================= LEVEL 3 ================= */
  getOperations(serial: string): any[] {
    const s = this.findSerial(serial);
    if (!s) return [];

    return s.operations.map((o: any) => ({
      operNo: o.operation,
      employees: o.employee ? [o.employee] : [],
      start: o.startTime,
      end: o.endTime,
      status: o.status, // Use actual API status
      runningHrs: this.decimalHoursToHHMMSS(o.hoursConsumed)
    }));
  }

  /** ================= HELPERS ================= */
  private findSerial(serial: string): any {
    for (const job of this.jobProgressData) {
      const s = job.serials.find((x: any) => x.serialNo === serial);
      if (s) return s;
    }
    return null;
  }

  private calculateJobTotalHours(job: any): string {
    const totalDecimalHours = job.serials.reduce(
      (sum: number, s: any) => sum + (s.totalHours || 0),
      0
    );

    return this.decimalHoursToHHMMSS(totalDecimalHours);
  }



  /** ================= STATUS COLOR ================= */
  getStatusClass(status: string | null): string {
    if (!status) return 'status-default';

    switch (status.toLowerCase()) {
      case 'running': return 'status-running';
      case 'pause': return 'status-pause';
      case 'closed': return 'status-closed';
      case 'no transaction yet': return 'status-not-started';
      default: return 'status-default';
    }
  }
  /** Convert decimal HOURS → HH:mm:ss */
private decimalHoursToHHMMSS(hours: number): string {
  if (!hours || hours <= 0) return '00:00:00';

  const totalSeconds = Math.round(hours * 3600);

  const hh = Math.floor(totalSeconds / 3600);
  const mm = Math.floor((totalSeconds % 3600) / 60);
  const ss = totalSeconds % 60;

  return `${hh.toString().padStart(2, '0')}:` +
         `${mm.toString().padStart(2, '0')}:` +
         `${ss.toString().padStart(2, '0')}`;
}



  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }
}
