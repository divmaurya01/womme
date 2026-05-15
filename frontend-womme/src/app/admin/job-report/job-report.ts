import { Component, HostListener, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Table, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';

/* ── Interfaces ─────────────────────────────────────────────────────────────── */

interface EmployeeHours {
  empNum:    string;
  empName:   string;
  hours:     number;
  startTime: string;
  endTime:   string;
}

interface Operation {
  operNo:     number | string;
  wc:         string;
  employees:  EmployeeHours[];
  start:      string;
  end:        string;
  status:     string;
  runningHrs: string;
}

interface JobRow {
  job:      string;
  item:     string;
  itemDesc: string;
  qty:      number;
  jobDate:  string;
  totalHrs: string;
}

/* ── Component ──────────────────────────────────────────────────────────────── */

@Component({
  selector:    'app-job-report',
  templateUrl: './job-report.html',
  styleUrls:   ['./job-report.scss'],
  standalone:  true,
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
  isLoading       = true;
  searchTerm      = '';

  jobProgressData: any[]  = [];
  jobs: JobRow[]          = [];
  expandedSerial: { [serial: string]: boolean } = {};

  constructor(private jobService: JobService) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.loadJobProgress();
  }

  @HostListener('window:resize')
  onResize() { this.checkScreenSize(); }

  checkScreenSize() {
    this.isSidebarHidden = window.innerWidth <= 1024;
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  /* ── Search ───────────────────────────────────────────────────────────────── */

  onGlobalSearch(event: Event): void {
    const val = (event.target as HTMLInputElement).value.trim();
    if (!val) { this.dt.clear(); return; }
    this.dt.filterGlobal(val, 'contains');
  }

  /* ── Data load ────────────────────────────────────────────────────────────── */

  loadJobProgress(): void {
    this.isLoading = true;
    this.jobService.getJobProgress().subscribe({
      next: (res: any[]) => {
        this.jobProgressData = res;
        this.jobs = res.map(j => ({
          job:      j.jobNo,
          item:     j.item        ?? '-',
          itemDesc: j.description ?? '-',
          qty:      j.qty,
          jobDate:  j.jobDate,
          totalHrs: this.fmt(j.totalHours ?? 0)
        }));
        this.isLoading = false;
      },
      error: () => (this.isLoading = false)
    });
  }

  /* ── Serial helpers ───────────────────────────────────────────────────────── */

  getSerials(job: JobRow): string[] {
    const found = this.jobProgressData.find(j => j.jobNo === job.job);
    return found ? found.serials.map((s: any) => s.serialNo) : [];
  }

  toggleSerial(serial: string, e: Event): void {
    e.stopPropagation();
    this.expandedSerial[serial] = !this.expandedSerial[serial];
  }

  getTotalOperation(serial: string):     string | number { return this.findSerial(serial)?.totalOperations     ?? '-'; }
  getRunningOperation(serial: string):   string | number { return this.findSerial(serial)?.runningOperations   ?? '-'; }
  getCompletedOperation(serial: string): string | number { return this.findSerial(serial)?.completedOperations ?? '-'; }

  getSerialRunningHrs(serial: string): string {
    return this.fmt(this.findSerial(serial)?.totalHours ?? 0);
  }

  /* ── Operations ───────────────────────────────────────────────────────────── */

  getOperations(serial: string): Operation[] {
    const s = this.findSerial(serial);
    if (!s) return [];

    return s.operations.map((o: any) => {
      let employees: EmployeeHours[] = (o.employees ?? []).map((e: any) => ({
        empNum:    e.empNum    ?? '',
        empName:   e.empName   ?? e.empNum ?? '',
        hours:     e.hours     ?? 0,
        startTime: e.startTime ?? '-',
        endTime:   e.endTime   ?? '-'
      }));

      // Fallback: old single/pipe-joined API shape
      if (employees.length === 0 && (o.employee || o.employeeName)) {
        const raw   = (o.employeeName || o.employee || '').trim();
        const names = raw ? raw.split('|').map((n: string) => n.trim()).filter(Boolean) : [];
        employees   = names.map((name: string) => ({
          empNum:    '',
          empName:   name,
          hours:     names.length === 1 ? (o.hoursConsumed ?? 0) : 0,
          startTime: o.startTime ?? '-',
          endTime:   o.endTime   ?? '-'
        }));
      }

      return {
        operNo:     o.operation,
        wc:         o.wc       ?? '-',
        employees,
        start:      o.startTime ?? '-',
        end:        o.endTime   ?? '-',
        status:     o.status    ?? 'Unknown',
        runningHrs: this.fmt(o.hoursConsumed ?? 0)
      };
    });
  }

  getEmployeeNames(op: Operation): string[] {
    return op.employees.map(e => (e.empName || e.empNum).trim()).filter(Boolean);
  }

  /* ── Status CSS class ─────────────────────────────────────────────────────── */

  getStatusClass(status: string): string {
    const map: Record<string, string> = {
      'Running':            'status-running',
      'Paused':             'status-paused',
      'Completed':          'status-done',
      'Hold':               'status-hold',
      'Rejected':           'status-rejected',
      'In Queue':           'status-queue',
      'No Transaction Yet': 'status-none',
      'Unknown':            'status-none'
    };
    return map[status] ?? 'status-none';
  }

  /* ── Excel export — mirrors UI 3-level structure exactly ─────────────────── */

  exportToExcel(): void {
    import('xlsx').then(XLSX => {

      // Only export rows currently visible after search/filter
      const visibleJobRows: JobRow[] =
        (this.dt.filteredValue as JobRow[] | null) ?? this.jobs;

      const visibleJobNos  = new Set(visibleJobRows.map(r => r.job));
      const visibleRawJobs = this.jobProgressData.filter(j => visibleJobNos.has(j.jobNo));

      const wb   = XLSX.utils.book_new();
      const rows: any[][] = [];

      // ── COLOUR constants (ARGB hex, no #) ──────────────────────────────
      const COLOR_JOB     = 'FFECC391'; // orange-beige  — Level 1 header bg
      const COLOR_SERIAL  = 'FFF4E0C8'; // light peach   — Level 2 header bg
      const COLOR_OP      = 'FFD8F8EE'; // mint green    — Level 3 header bg
      const COLOR_WHITE   = 'FFFFFFFF';
      const COLOR_STRIPE  = 'FFFFF9F4'; // very light stripe for job data rows

      const hdrFont   = { bold: true, sz: 11 };
      const bodyFont  = { sz: 10 };
      const center    = { horizontal: 'center', vertical: 'middle' };
      const left      = { horizontal: 'left',   vertical: 'middle' };
      const wrapLeft  = { horizontal: 'left',   vertical: 'top', wrapText: true };

      // Helper: build a cell style
      const cs = (bgColor: string, font: any, alignment: any, border = false) => ({
        fill:      { patternType: 'solid', fgColor: { rgb: bgColor } },
        font,
        alignment,
        border: border ? {
          top:    { style: 'thin', color: { rgb: 'FFD0D0D0' } },
          bottom: { style: 'thin', color: { rgb: 'FFD0D0D0' } },
          left:   { style: 'thin', color: { rgb: 'FFD0D0D0' } },
          right:  { style: 'thin', color: { rgb: 'FFD0D0D0' } }
        } : {}
      });

      // Row-index tracker (0-based, incremented as we push rows)
      let ri = 0;

      // Merge ranges collected for the sheet
      const merges: any[] = [];

      // ── Push a single row and return its index ──────────────────────────
      const pushRow = (cells: any[]) => {
        rows.push(cells);
        return ri++;
      };

      // ── Level-1 header (Job table header — matches HTML main-header) ────
      const jobHdrStyle = cs(COLOR_JOB, hdrFont, center, true);
      pushRow([
        { v: 'Job No',      s: jobHdrStyle },
        { v: 'Item',        s: jobHdrStyle },
        { v: 'Description', s: jobHdrStyle },
        { v: 'Qty',         s: jobHdrStyle },
        { v: 'Job Date',    s: jobHdrStyle },
        { v: 'Total Hrs',   s: jobHdrStyle },
        { v: '', s: jobHdrStyle }, // col G (placeholder so merged cols look right)
      ]);

      visibleRawJobs.forEach((job: any) => {
        const jobRow = visibleJobRows.find(r => r.job === job.jobNo)!;

        // ── Level-1 data row ──────────────────────────────────────────────
        const jobDataStyle = cs(COLOR_STRIPE, { ...bodyFont, bold: false }, left, true);
        const jobBoldStyle = cs(COLOR_STRIPE, { ...bodyFont, bold: true  }, left, true);
        pushRow([
          { v: jobRow.job,      s: jobDataStyle },
          { v: jobRow.item,     s: jobDataStyle },
          { v: jobRow.itemDesc, s: jobDataStyle },
          { v: jobRow.qty,      s: jobDataStyle },
          { v: jobRow.jobDate
              ? new Date(jobRow.jobDate).toLocaleDateString('en-GB')
              : '-',            s: jobDataStyle },
          { v: jobRow.totalHrs, s: jobBoldStyle },
          { v: '',              s: jobDataStyle },
        ]);

        // ── Level-2 header (Serial table header) ─────────────────────────
        const serialHdrStyle = cs(COLOR_SERIAL, hdrFont, center, true);
        pushRow([
          { v: 'Serial',      s: serialHdrStyle },
          { v: 'Total Ops',   s: serialHdrStyle },
          { v: 'Running',     s: serialHdrStyle },
          { v: 'Completed',   s: serialHdrStyle },
          { v: 'Running Hrs', s: serialHdrStyle },
          { v: '',            s: serialHdrStyle },
          { v: '',            s: serialHdrStyle },
        ]);

        job.serials.forEach((serial: any) => {
          const serialHrs = this.fmt(serial.totalHours ?? 0);

          // ── Level-2 data row (Serial) ─────────────────────────────────
          const serialDataStyle = cs(COLOR_WHITE, bodyFont, left, true);
          pushRow([
            { v: serial.serialNo,             s: serialDataStyle },
            { v: serial.totalOperations,      s: { ...serialDataStyle, alignment: center } },
            { v: serial.runningOperations,    s: { ...serialDataStyle, alignment: center } },
            { v: serial.completedOperations,  s: { ...serialDataStyle, alignment: center } },
            { v: serialHrs,                   s: serialDataStyle },
            { v: '',                          s: serialDataStyle },
            { v: '',                          s: serialDataStyle },
          ]);

          // ── Level-3 header (Operation table header) ───────────────────
          const opHdrStyle = cs(COLOR_OP, hdrFont, center, true);
          pushRow([
            { v: 'Operation',   s: opHdrStyle },
            { v: 'WC',          s: opHdrStyle },
            { v: 'Employee(s)', s: opHdrStyle },
            { v: 'Start',       s: opHdrStyle },
            { v: 'End',         s: opHdrStyle },
            { v: 'Status',      s: opHdrStyle },
            { v: 'Hrs',         s: opHdrStyle },
          ]);

          const operations = this.getOperations(serial.serialNo);

          operations.forEach(op => {
            const names    = this.getEmployeeNames(op);
            const empCell  = names.length ? names.join('\n') : '-';
            const hrsCell  = op.employees.length > 1
              ? op.employees
                  .map(e => `${e.empName || e.empNum}: ${this.fmt(e.hours)}`)
                  .join('\n')
              : op.runningHrs;

            const opDataStyle  = cs(COLOR_WHITE, bodyFont, left,     true);
            const opWrapStyle  = cs(COLOR_WHITE, bodyFont, wrapLeft, true);
            const opCtrStyle   = cs(COLOR_WHITE, bodyFont, center,   true);

            pushRow([
              { v: op.operNo,  s: opCtrStyle  },
              { v: op.wc,      s: opDataStyle },
              { v: empCell,    s: opWrapStyle },  // multi-name wrap
              { v: op.start,   s: opCtrStyle  },
              { v: op.end === '-' ? '-' : op.end, s: opCtrStyle },
              { v: op.status,  s: opDataStyle },
              { v: hrsCell,    s: opWrapStyle },  // multi-emp hrs wrap
            ]);
          });

          // Blank spacer row between serials
          pushRow([{ v: '' }, { v: '' }, { v: '' }, { v: '' }, { v: '' }, { v: '' }, { v: '' }]);
        });

        // Blank spacer row between jobs
        pushRow([{ v: '' }, { v: '' }, { v: '' }, { v: '' }, { v: '' }, { v: '' }, { v: '' }]);
      });

      // ── Build worksheet from rows array ───────────────────────────────
      const ws = XLSX.utils.aoa_to_sheet(rows);
      ws['!merges'] = merges;

      // Column widths (A–G)
      ws['!cols'] = [
        { wch: 18 }, // A — Serial / Job No / Operation
        { wch: 14 }, // B — Item / Total Ops / WC
        { wch: 30 }, // C — Description / Running / Employee(s)
        { wch: 10 }, // D — Qty / Completed / Start
        { wch: 13 }, // E — Job Date / Running Hrs / End
        { wch: 22 }, // F — Total Hrs / Status
        { wch: 18 }, // G — Hrs
      ];

      XLSX.utils.book_append_sheet(wb, ws, 'Job Report');

      const term   = this.searchTerm.trim();
      const suffix = term ? `_${term}` : '';
      XLSX.writeFile(wb, `JobReport${suffix}_${new Date().toISOString().slice(0, 10)}.xlsx`);
    });
  }

  /* ── Public hrs formatter (called from template) ──────────────────────────── */
  fmtHrs(hours: number): string { return this.fmt(hours); }

  /* ── Private helpers ──────────────────────────────────────────────────────── */

  private findSerial(serial: string): any {
    for (const job of this.jobProgressData) {
      const s = job.serials.find((x: any) => x.serialNo === serial);
      if (s) return s;
    }
    return null;
  }

  private fmt(hours: number): string {
    if (!hours || hours <= 0) return '00:00:00';
    const t  = Math.round(hours * 3600);
    const hh = Math.floor(t / 3600);
    const mm = Math.floor((t % 3600) / 60);
    const ss = t % 60;
    return `${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}:${String(ss).padStart(2, '0')}`;
  }
}