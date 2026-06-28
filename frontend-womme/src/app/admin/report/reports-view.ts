import { Component, OnInit, Input, HostListener, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { JobService } from '../../services/job.service';
import { ReportService } from '../../services/report.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import html2pdf from 'html2pdf.js';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';

interface JobItem {
  seq: number;
  item: string;
  description: string;
  qty: number;
  serialNumber?: string;
  remark?: string;
}

interface JobOperationFrontend {
  operationNo: string;
  wc: string;
  description: string;
  transactions: JobTransaction[];
  items: JobItem[];
  qrCodeUrl?: string | null;
}

interface JobTransaction {
  serialNo: string;
  qty: number;
  date: string;
  notes: string;
  machineId?: string;
  employeeId?: string;
  employeeName?: string;
}

@Component({
  selector: 'app-reports-view',
  templateUrl: './reports-view.html',
  styleUrls: ['./reports-view.scss'],
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
export class ReportsViewComponent implements OnInit, OnDestroy {

  isSidebarHidden = window.innerWidth <= 1024;

  @Input() jobId: string | null = null;
  jobNumber: string = '';
  qrCodeUrl: string | null = null;

  @Input() jobData: any = {};
  @Input() jobOperations: JobOperationFrontend[] = [];

  constructor(
    private jobService: JobService,
    private route: ActivatedRoute,
    private reportService: ReportService,
    private loader: LoaderService
  ) {}

  // ─── Lifecycle ────────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.checkScreenSize();
    this.loader.show();

    this.route.queryParams
      .pipe(finalize(() => this.loader.hide()))
      .subscribe(params => {
        const jobId = params['jb_id'] || null;
        if (jobId) {
          this.jobNumber = jobId;
          this.loadJobReport(jobId);
        }
      });
  }

  ngOnDestroy(): void {
    if (this.qrCodeUrl) URL.revokeObjectURL(this.qrCodeUrl);
  }

  // ─── Screen / Sidebar ─────────────────────────────────────────────────────

  @HostListener('window:resize')
  onResize(): void { this.checkScreenSize(); }

  checkScreenSize(): void {
    this.isSidebarHidden = window.innerWidth <= 1024;
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  // ─── Load Data ────────────────────────────────────────────────────────────

  loadJobReport(jobId: string | null, download: boolean = false): Promise<void> {
    return new Promise((resolve) => {
      if (!jobId) { resolve(); return; }

      this.loader.show();

      this.jobService.GetJobReport(jobId)
        .pipe(finalize(() => this.loader.hide()))
        .subscribe({
          next: (data) => {
            if (!data) { resolve(); return; }

            // ── Map job header ───────────────────────────────────────────────
            this.jobData = {
                        job              : data.job,
                        jobDate          : data.jobDate          ? data.jobDate.split('T')[0]          : '',
                        jobDueDate       : data.jobDueDate       ? data.jobDueDate.split('T')[0]       : '',
                        RevisionDate     : data.revisionDate     ? data.revisionDate.split('T')[0]     : '',
                        date             : data.date             ? data.date.split('T')[0]             : '',
                        preparedBy       : data.preparedBy       || '---',
                        materialClass    : data.materialClass    || '---',
                        drawingNo        : data.drawingNo        || '---',
                        revisionNo       : data.revisionNo       || '---',
                        tempClass        : data.tempClass        || '---',
                        drawingRev       : data.drawingRev       || 0,
                        soNo             : data.soNo             || '---',
                        released         : data.releasedQty      || 0,
                        status           : this.mapStatus(data.status),
                        matlDesc         : data.matlDesc         || '---',
                        psl              : data.psl              || '---',
                        item             : data.item             || '---',
                        description      : data.itemDescription  || '---',
                        suffix           : data.suffix           ?? 0,
                        ufItemDescription2: data.ufItemDescription2 || '---',
                        formNo_revno     : data.formNo_revno     || '',

                        // ── NEW ──────────────────────────────────────
                        routerRevRemarks : data.routerRevRemarks || '---',
                        addJobCORef      : data.addJobCORef      || '---',
                        heatNo           : data.heatNo           || '---',
                        heatCode         : data.heatCode         || '---',
                        specNoJob        : data.specNoJob        || '---',
                        // ─────────────────────────────────────────────

                        // ── These are in HTML but NOT coming from API yet ──
                        remark           : data.remark           || '',
                        engineeringNotes : data.engineeringNotes || '',
                        thirdPartyInsp   : data.thirdPartyInsp   || '---',
                        traceCode        : data.traceCode        || '---',  // maps to heatCode?
                        partRev          : data.partRev          || '---',
                        specNo           : data.specNoJob        || '---',  // ← maps to SpecNoJob
                      };

            // ── Map operations ───────────────────────────────────────────────
            this.jobOperations = (data.operations || []).map((op: any) => {
              const transArray = op.transactions || op.Transactions || [];

              const machines  = new Set<string>();
              const employees = new Set<string>();

              transArray.forEach((t: any) => {
                if (t.machineId)    t.machineId.split(',').forEach((m: string) => machines.add(m.trim()));
                if (t.employeeName) t.employeeName.split(',').forEach((e: string) => employees.add(e.trim()));
              });

              const machineStr  = Array.from(machines).join(', ');
              const employeeStr = Array.from(employees).join(', ');

              const transactions = transArray
                .filter((t: any) => t.status === '3')
                .map((t: any) => ({
                  serialNo    : t.serialNo   || '---',
                  qty         : 1,
                  date        : t.transDate  ? t.transDate.split('T')[0] : '---',
                  notes       : t.remark     || '',
                  machineId   : machineStr,
                  employeeName: employeeStr
                }));

              return {
                operationNo : op.operNum?.toString() || '---',
                description : op.operationDescription || '',
                wc          : op.wc  || '',
                items       : (op.items || []).map((it: any) => ({
                  seq         : it.sequence  || it.seq  || 0,
                  item        : it.item      || it.itemCode || '---',
                  description : `${it.itemDescription || ''} ${it.ufItemDescription2 || ''}`.trim(),
                  qty         : it.requiredQty || it.qty || 0,
                  remark      : it.ufLastVendName || ''
                })),
                transactions,
                qrCodeUrl: op.qrCodeUrl || null
              };
            });

            // ── Fetch Job QR ─────────────────────────────────────────────────
            if (data.job) {
              this.loader.show();
              this.jobService.GenerateQrWithJob(data.job)
                .pipe(finalize(() => this.loader.hide()))
                .subscribe({
                  next : (blob) => { this.qrCodeUrl = URL.createObjectURL(blob); },
                  error: ()     => { this.qrCodeUrl = null; }
                });
            }

            // ── Fetch Operation QR codes ─────────────────────────────────────
            this.jobOperations.forEach(op => {
              this.loader.show();
              this.jobService.downloadOperationQR(op.operationNo)
                .pipe(finalize(() => this.loader.hide()))
                .subscribe({
                  next : (blob) => { op.qrCodeUrl = URL.createObjectURL(blob); },
                  error: ()     => { op.qrCodeUrl = null; }
                });
            });

            resolve();
          },
          error: (err) => { console.error('API Error:', err); resolve(); }
        });
    });
  }

  // ─── Helpers ──────────────────────────────────────────────────────────────

  private mapStatus(code: string): string {
    switch (code) {
      case 'R': return 'Released';
      case 'C': return 'Closed';
      case 'O': return 'Open';
      case 'H': return 'Hold';
      default:  return code || '---';
    }
  }

  // ─── PDF Download with page numbers ──────────────────────────────────────

  downloadPDF(): void {
    const element = document.querySelector('.report-wrapper') as HTMLElement;
    if (!element) return;

    const opt = {
      margin  : [20, 20, 30, 20],
      filename: `${this.jobData.job}_Report.pdf`,
      image   : { type: 'jpeg', quality: 0.98 },
      html2canvas: {
        scale: 2,
        useCORS: true,
        scrollY: 0,
        windowWidth: element.scrollWidth,
        windowHeight: element.scrollHeight
      },
      jsPDF   : { unit: 'pt', format: 'a4', orientation: 'portrait' },
      pagebreak: {
        mode: ['avoid-all', 'css', 'legacy'],
        avoid: ['.header-table', '.meta-info-table', '.issuance-table', '.item-table']
      }
    };

    html2pdf()
      .set(opt)
      .from(element)
      .toPdf()
      .get('pdf')
      .then((pdf: any) => {
        const totalPages = pdf.internal.getNumberOfPages();
        const pageWidth  = pdf.internal.pageSize.getWidth();
        const pageHeight = pdf.internal.pageSize.getHeight();

        const formNo = this.jobData?.formNo_revno || '---';

        for (let i = 1; i <= totalPages; i++) {
          pdf.setPage(i);

          pdf.setFontSize(9);
          pdf.setTextColor(120);

          pdf.text(
            `Form & Rev No: ${formNo}`,
            20,
            pageHeight - 10
          );

          pdf.text(
            `Page ${i} of ${totalPages}`,
            pageWidth - 80,
            pageHeight - 10
          );
        }
      })
      .save();
  }
}