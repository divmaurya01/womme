import { Component, OnInit, Input, HostListener } from '@angular/core';
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
import { Theme } from 'fullcalendar';

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
  items: JobItem[];
  qrCodeUrl?: string | null;
}
interface JobTransaction {
  serialNo: string;
  qty: number;
  date: string;
  notes: string;
}

interface JobOperationFrontend {
  operationNo: string;
  description: string;
  items: JobItem[];
  transactions: JobTransaction[]; // ✅ add this line
  qrCodeUrl?: string | null;
}

@Component({
  selector: 'app-reports-view',
  templateUrl: './reports-view.html',
  styleUrls: ['./reports-view.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, HeaderComponent, SidenavComponent, TableModule, DialogModule]
})
export class ReportsViewComponent implements OnInit {
  isSidebarHidden = window.innerWidth <= 1024;
  @Input()jobId: string | null = null;
  jobNumber: string = '';
  qrCodeUrl: string | null = null;
  @Input() jobData: any = {};
  @Input() jobOperations: JobOperationFrontend[] = [];

  constructor(
    private jobService: JobService,
    private route: ActivatedRoute,
    private reportService: ReportService,private loader:LoaderService
  ) {}

  private mapStatus(code: string): string {
    switch (code) {
      case "R": return "Released";
      case "C": return "Closed";
      case "O": return "Open";
      case "H": return "Hold";
      default:  return code || "---";
    }
  }

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

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  loadJobReport(jobId: string | null, download: boolean = false): Promise<void> {
    return new Promise((resolve) => {
      if (!jobId) {
        resolve();
        return;
      }

      this.loader.show();

      this.jobService.GetJobReport(jobId)
        .pipe(finalize(() => this.loader.hide()))
        .subscribe({
          next: (data) => {
            if (!data) {
              resolve();
              return;
            }

            //  Job header mapping
            this.jobData = {
              job: data.job,
              jobDate: data.jobDate ? new Date(data.jobDate).toLocaleDateString() : '---',
              jobDueDate: data.jobDueDate ? new Date(data.jobDueDate).toLocaleDateString() : '---',
              preparedBy: data.preparedBy || '---',
              materialClass: data.materialClass || '---',
              drawingNo: data.drawingNo || '---',
              revisionNo: data.revisionNo || '---',
              tempClass: data.tempClass || '---',
              drawingRev: data.drawingRev || 0,
              soNo: data.soNo || '---',
              released: data.releasedQty || 0,
              status: this.mapStatus(data.status),
              matlDesc: data.matlDesc || '---',
              psl: data.psl || '---',
              item: data.item || '---',
              description: data.itemDescription || '---',
              suffix: data.suffix ?? 0,
              ufItemDescription2: data.ufItemDescription2 || '---',
              date: data.createdDate ? new Date(data.createdDate).toLocaleDateString() : '---'
            };


            //  Operation mapping starts
            this.jobOperations = (data.operations || []).map((op: any, opIndex: number) => {

              // ✅ Detect transactions array
              const transArray = op.transactions || op.Transactions || [];

              // ✅ Check type & length
              if (!Array.isArray(transArray)) {
                console.warn(` Transactions is not an array for Operation ${op.operNum}`);
              } else {
                console.log(` Found ${transArray.length} transactions for Operation ${op.operNum}`);
              }

              const transactions = Array.isArray(transArray) && transArray.length > 0
                ? transArray.map((t: any, tIndex: number) => {
                    return {
                      serialNo: t.serialNo || t.SerialNo || '---',
                      qty: 1, // fixed as required
                      date: t.createDate
                        ? new Date(t.createDate).toISOString().split('T')[0]
                        : '---',
                      notes: t.remark||'---'
                    };
                  })
                : [];


              const mappedOp = {
                operationNo: op.operNum?.toString() || op.operationNo || '---',
                description: op.operationDescription || op.description || '---',
                wc: op.wc || op.wc || '---',
                items: (op.items || []).map((it: any, i: number) => ({
                  seq: it.sequence || it.seq || 0,
                  item: it.item || it.itemCode || '---',
                  description: `${it.itemDescription || ''} ${it.ufItemDescription2 || ''}`.trim(),
                  qty: it.requiredQty || it.qty || 0,
                  remark: it.ufLastVendName || ''
                })),
                transactions,
                qrCodeUrl: op.qrCodeUrl || null
              };

              console.groupEnd();
              return mappedOp;
            });
            

            //  Fetch Job QR
            if (data.job) {
              this.loader.show();
              this.jobService.GenerateQrWithJob(data.job)
                .pipe(finalize(() => this.loader.hide()))
                .subscribe({
                  next: (blob) => { this.qrCodeUrl = URL.createObjectURL(blob); },
                  error: () => { this.qrCodeUrl = null; }
                });
            }

            //  Fetch Operation QR codes
            this.jobOperations.forEach(op => {
              this.loader.show();
              this.jobService.downloadOperationQR(op.operationNo)
                .pipe(finalize(() => this.loader.hide()))
                .subscribe({
                  next: (blob) => { op.qrCodeUrl = URL.createObjectURL(blob); },
                  error: () => { op.qrCodeUrl = null; }
                });
            });

            resolve();
          },
          error: (err) => {
            console.error(' API Error:', err);
            resolve();
          }
        });
    });
  }
  ngOnDestroy() {
    if (this.qrCodeUrl) {
      URL.revokeObjectURL(this.qrCodeUrl);
    }
  }

downloadPDF() {
  const element = document.querySelector('.report-wrapper');
  if (!element) return;

  const opt = {
    margin: [20, 20, 20, 20], // Adds space around content
    filename: `${this.jobData.job}_Report.pdf`,
    image: { type: 'jpeg', quality: 0.98 },
    html2canvas: { scale: 2, useCORS: true, scrollY: 0 },
    jsPDF: { unit: 'pt', format: 'a4', orientation: 'portrait' },
    pagebreak: { mode: ['avoid-all', 'css', 'legacy'] } // ✅ Prevent splits
  };

  html2pdf().set(opt).from(element).save();
}

}
