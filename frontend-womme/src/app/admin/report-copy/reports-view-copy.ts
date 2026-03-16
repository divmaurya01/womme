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
  selector: 'app-reports-view-copy',
  templateUrl: './reports-view-copy.html',
  styleUrls: ['./reports-view-copy.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, HeaderComponent, SidenavComponent, TableModule, DialogModule]
})
export class ReportsViewComponentCopy implements OnInit {
  isSidebarHidden = window.innerWidth <= 1024;
  isEditMode = false;
  auditData: any = {};
  originalJobData: any = {};
  originalOperations: JobOperationFrontend[] = [];
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

  enableEdit() {
    this.isEditMode = true;
    this.auditData = { ...this.jobData };
  }


  saveAudit() {

  const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
  const updatedBy = userDetails.employeeCode || '';

  const fields: any = {};
  const transactions: any[] = [];

  // 🔹 Detect header changes automatically
  Object.keys(this.auditData).forEach(key => {

    if (this.auditData[key] !== this.originalJobData[key]) {

      fields[key] = this.auditData[key];

    }

  });


  // 🔹 Detect transaction changes automatically
  this.jobOperations.forEach((op, opIndex) => {

    const originalOp = this.originalOperations[opIndex];
    if (!originalOp) return;

    op.transactions?.forEach((t, tIndex) => {

      const originalT = originalOp.transactions[tIndex];
      if (!originalT) return;

      (Object.keys(t) as (keyof JobTransaction)[]).forEach(col => {

        if (t[col] !== originalT[col]) {

          transactions.push({
            operationNo: op.operationNo,
            serialNo: t.serialNo,
            columnName: col,
            columnValue: t[col]
          });

        }

      });

    });

  });


  const payload = {
    job: this.jobData.job,
    updatedBy,
    fields,
    transactions
  };

  console.log("Payload:", payload);

  if (Object.keys(fields).length === 0 && transactions.length === 0) {
    alert("No changes detected");
    return;
  }

  this.jobService.saveJobAudit(payload).subscribe({
    next: () => {
      this.isEditMode = false;
      this.loadJobReport(this.jobNumber);
    }
  });

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
              jobDate: data.jobDate ? data.jobDate.split('T')[0] : '',
              jobDueDate: data.jobDueDate ? data.jobDueDate.split('T')[0] : '',
              RevisionDate: data.revisionDate ? data.revisionDate.split('T')[0] : '',
              date: data.date ? data.date.split('T')[0] : '',
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
              
            };


              //  Operation mapping starts
              this.jobOperations = (data.operations || []).map((op: any, opIndex: number) => {

              //  Detect transactions array
              const transArray = op.transactions || op.Transactions || [];

              
              //  Check type & length
              if (!Array.isArray(transArray)) {
                console.warn(` Transactions is not an array for Operation ${op.operNum}`);
              } else {
                console.log(` Found ${transArray.length} transactions for Operation ${op.operNum}`);
              }

              const machines = new Set<string>();
              const employees = new Set<string>();

              transArray.forEach((t: any) => {

                if (t.machineId) {
                  t.machineId.split(',').forEach((m: string) => machines.add(m.trim()));
                }

                if (t.employeeName) {
                  t.employeeName.split(',').forEach((e: string) => employees.add(e.trim()));
                }

              });

              const machineStr = Array.from(machines).join(', ');
              const employeeStr = Array.from(employees).join(', ');

              const transactions = transArray
                .filter((t: any) => t.status === '3')   // keep completed row for table
                .map((t: any) => ({
                  serialNo: t.serialNo || '---',
                  qty: 1,
                  date: t.transDate ? t.transDate.split('T')[0] : '---',
                  notes: t.remark || '',
                  machineId: machineStr,
                  employeeName: employeeStr
                }));

               const mappedOp = {
                operationNo: op.operNum?.toString() || '---',
                description: op.operationDescription || '',
                wc: op.wc || '',
                machineId: op.machineId || '',
                employeeId: op.employeeId || '',
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
            
            this.originalJobData = JSON.parse(JSON.stringify(this.jobData));
            this.originalOperations = JSON.parse(JSON.stringify(this.jobOperations));

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
