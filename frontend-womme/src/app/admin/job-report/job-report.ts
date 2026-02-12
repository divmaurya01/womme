import { Component, HostListener, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Table, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';

@Component({
  selector: 'app-job-report',
  standalone: true,
  templateUrl: './job-report.html',
  styleUrls: ['./job-report.scss'],
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    HeaderComponent,
    SidenavComponent,
    ButtonModule
  ]
})
export class JobReportComponent implements OnInit {

  @ViewChild('dt') dt!: Table;

 isSidebarHidden = window.innerWidth <= 1024;
  isLoading = true;
  searchTerm = '';

  jobProgressData: any[] = [];
  jobs: any[] = [];
  expandedSerial: { [serial: string]: boolean } = {};

  constructor(private jobService: JobService) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.loadJobProgress();
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

  onGlobalSearch(event: Event): void {
    const input = (event.target as HTMLInputElement).value.trim();
    if (!input) {
      this.dt.clear();
      return;
    }
    this.dt.filterGlobal(input, 'contains');
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
          jobDate: j.jobDate,
          totalHrs: this.calculateJobTotalHours(j)
        }));
        this.isLoading = false;
      },
      error: () => (this.isLoading = false)
    });
  }

  getSerials(job: any): string[] {
    const jobData = this.jobProgressData.find(j => j.jobNo === job.job);
    return jobData ? jobData.serials.map((s: any) => s.serialNo) : [];
  }

  toggleSerial(serial: string, event: Event) {
    event.stopPropagation();
    this.expandedSerial[serial] = !this.expandedSerial[serial];
  }

  getTotalOperation(serial: string): string {
    return this.findSerial(serial)?.totalOperations ?? '-';
  }

  getRunningOperation(serial: string): string {
    return this.findSerial(serial)?.runningOperations ?? '-';
  }

  getCompletedOperation(serial: string): string {
    return this.findSerial(serial)?.completedOperations ?? '-';
  }

  getSerialRunningHrs(serial: string): string {
    return this.decimalHoursToHHMMSS(this.findSerial(serial)?.totalHours ?? 0);
  }

  getOperations(serial: string): any[] {
    const s = this.findSerial(serial);
    return s
      ? s.operations.map((o: any) => ({
          operNo: o.operation,
          employees: o.employee ? [o.employee] : [],
          start: o.startTime,
          end: o.endTime,
          status: o.status,
          runningHrs: this.decimalHoursToHHMMSS(o.hoursConsumed)
        }))
      : [];
  }

  private findSerial(serial: string): any {
    for (const job of this.jobProgressData) {
      const s = job.serials.find((x: any) => x.serialNo === serial);
      if (s) return s;
    }
    return null;
  }

  private calculateJobTotalHours(job: any): string {
    const hours = job.serials.reduce(
      (sum: number, s: any) => sum + (s.totalHours || 0),
      0
    );
    return this.decimalHoursToHHMMSS(hours);
  }

  private decimalHoursToHHMMSS(hours: number): string {
    if (!hours) return '00:00:00';
    const totalSeconds = Math.round(hours * 3600);
    const hh = Math.floor(totalSeconds / 3600);
    const mm = Math.floor((totalSeconds % 3600) / 60);
    const ss = totalSeconds % 60;
    return `${hh.toString().padStart(2, '0')}:${mm
      .toString()
      .padStart(2, '0')}:${ss.toString().padStart(2, '0')}`;
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }
}
