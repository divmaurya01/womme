import { Injectable } from '@angular/core';
import { JobService } from './job.service';
import html2pdf from 'html2pdf.js';
import html2canvas from 'html2canvas';
import jsPDF from 'jspdf';
@Injectable({
  providedIn: 'root'
})
export class ReportService {
  jobData: any;
  jobOperations: any[] = [];
  qrCodeUrl: string | null = null;

  constructor(private jobService: JobService) {}

  // --- Load Job Report + Download if asked ---
  loadJobReport(jobId: string | null, download: boolean = false): Promise<void> {
    return new Promise((resolve) => {
      if (!jobId) {
        resolve();
        return;
      }

      this.jobService.GetJobReport(jobId).subscribe({
        next: (data) => {
          if (!data) {
            resolve();
            return;
          }

          // --- Map job data ---
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

          // --- Operations ---
          this.jobOperations = (data.operations || []).map((op: any) => ({
            operationNo: op.operNum.toString(),
            description: op.operationDescription || '---',
            items: (op.items || []).map((it: any) => ({
              seq: it.sequence,
              item: it.item,
              description: `${it.itemDescription} ${it.ufItemDescription2 || ''}`.trim(),
              qty: it.requiredQty,
              footer: it.ufLastVendName || '',
              remark: ''
            })),
            qrCodeUrl: null
          }));

          // --- Fetch Job QR ---
          if (data.job) {
            this.jobService.GenerateQrWithJob(data.job).subscribe({
              next: (blob) => { this.qrCodeUrl = URL.createObjectURL(blob); },
              error: () => { this.qrCodeUrl = null; }
            });
          }

          // --- Operation QR Codes ---
          this.jobOperations.forEach(op => {
            this.jobService.downloadOperationQR(op.operationNo).subscribe({
              next: (blob) => { op.qrCodeUrl = URL.createObjectURL(blob); },
              error: () => { op.qrCodeUrl = null; }
            });
          });

          // Download automatically if asked
          if (download) {
            setTimeout(() => this.downloadReportPDF(), 200);
          }

          resolve();
        },
        error: () => resolve()
      });
    });
  }

  // --- Status mapping helper ---
  private mapStatus(status: number): string {
    switch (status) {
      case 0: return 'Open';
      case 1: return 'Closed';
      default: return 'Unknown';
    }
  }

  downloadReportPDF() {
    const element = document.querySelector('.report-wrapper') as HTMLElement;
    if (!element) {
      console.error("Element .report-wrapper not found");
      return;
    }

    html2canvas(element).then((canvas) => {
      const imgData = canvas.toDataURL('image/png');
      const pdf = new jsPDF('p', 'mm', 'a4');

      const imgProps = (pdf as any).getImageProperties(imgData);
      const pdfWidth = pdf.internal.pageSize.getWidth();
      const pdfHeight = (imgProps.height * pdfWidth) / imgProps.width;

      pdf.addImage(imgData, 'PNG', 0, 0, pdfWidth, pdfHeight);
      pdf.save(`JobReport_${this.jobData?.job || 'Report'}.pdf`);
    });
  }
}
