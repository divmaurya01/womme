import { Component, HostListener, OnInit, ViewChild } from '@angular/core';
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
  templateUrl: './job-details.html',
  styleUrls: ['./job-details.scss'],
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
export class JobDetailComponent implements OnInit {
  isSidebarHidden = window.innerWidth <= 1024;
  jobDetails: any[] = [];
  loading = false;

  job: any;
  operation: any;
  releasedQty: any;
  machineNumber:any;
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
    this.checkScreenSize();
    const jobId = this.route.snapshot.queryParamMap.get('jb_id') || '0';
    const operNum = this.route.snapshot.queryParamMap.get('oper_num') || '0';
    const trans_num = this.route.snapshot.queryParamMap.get('trans_num') || '0';
    if (jobId && operNum && trans_num) this.getJobDetails(jobId, operNum, trans_num);
    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    
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

    getJobDetails(jobId: string, operNum: string, trans_num: string) {
      this.loading = true;

      this.jobService
        .GetJobUnpostedTransFullDetails(jobId, operNum, trans_num)
        .pipe(finalize(() => (this.loading = false)))
        .subscribe({
          next: (res) => {
            // Check for empty/null response
            if (res.length === 0) {
              this.jobDetails = [];
              this.job = '';
              this.operation = '';
              this.releasedQty = 0;
              return;
            }

            // Assign all rows to jobDetails for table binding
            this.jobDetails = res;
            const firstRow = res[0];
            this.job = firstRow.job || firstRow.Job;
            this.operation = firstRow.operNum || firstRow.OperNum;
            this.releasedQty = firstRow.releasedQty || firstRow.ReleasedQty;

            this.jobDetails.forEach((row: any) => {
              for (let i = 1; i <= row.releasedQty; i++) {
                const serialNo = `${row.transNum}_${i}`;
                
                this.jobService.EmpMechCodeChecker(
                  row.job.toString(),
                  String(row.operNum),         
                  row.transNum.toString(),     
                  serialNo
                )
                  .subscribe((result) => {
                    // attach employee/machine data to row object
                    row[`empCode_${i}`] = result.employeeCode;
                    row[`empName_${i}`] = result.employeeName;
                    row[`machineCode_${i}`] = result.machineNumber;
                    row[`machineDesc_${i}`] = result.machineDesc;
                    row[`status_${i}`] = result.status;
                    row[`statusTime_${i}`] = result.statusTime;

                    this.jobService.CheckJobStatus(
                        row.job.toString(),
                        String(row.operNum),
                        row.transNum.toString(),
                        serialNo
                      ).subscribe((logs) => {
                        const logsArray = Array.isArray(logs) ? logs : [logs];  // ensure array
                        this.processJobStatusLogs(row.transNum, i, logsArray);
                      });
                    
                  });
              }
            });


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


    processJobStatusLogs(transNum: string, serialIndex: number, logs: any[]) {
      const key = `${transNum}_${serialIndex}`;

      if (!logs || logs.length === 0) {
        this.updateRowStatus(transNum, serialIndex, 'NotStarted', null);
        this.elapsedTimes[key] = '00:00:00';
        return;
      }

      logs.sort((a, b) => new Date(a.statusTime).getTime() - new Date(b.statusTime).getTime());

      const latestLog = logs[logs.length - 1];
      const latestStatus = latestLog.status;

      this.updateRowStatus(transNum, serialIndex, latestStatus, latestLog.statusTime);

      let totalMs = 0;
      let startTime: Date | null = null;

      for (const log of logs) {
        if (log.status === 'Started') {
          startTime = new Date(log.statusTime);
        } else if ((log.status === 'Paused' || log.status === 'Completed') && startTime) {
          const endTime = new Date(log.statusTime);
          totalMs += endTime.getTime() - startTime.getTime();
          startTime = null;
        }
      }

      if (latestStatus === 'Started' && startTime) {
        this.startTimer(key, startTime.toISOString(), totalMs);
      } else {
        this.stopTimer(key);
        this.elapsedTimes[key] = this.formatTime(totalMs);
      }
    }

    private updateRowStatus(transNum: string, index: number, status: string, statusTime: string | null) {
      this.jobDetails.forEach(row => {
        if (row.transNum === transNum) {
          row[`status_${index}`] = status;
          row[`statusTime_${index}`] = statusTime;
        }
      });
    }




  downloadReport(){

  }

 
  
  startWizard(transNum: string, serialIndex: number, emp_code:string ) {
    this.selectedTransNum = transNum;
    this.selectedSerialNo = `${transNum}_${serialIndex}`;
    
    // store the full transaction row object
    this.selectedRow = this.jobDetails.find(j => j.transNum === transNum);
    if (this.selectedRow) {
      this.selectedRow.selectedSerialNo = this.selectedSerialNo;
      console.log("Selected Row:", this.selectedRow); 
    }

    const payload = {
      Job: this.selectedRow.job,
      Operation: String(this.selectedRow.operNum),  
      TransNum: String(this.selectedRow.transNum),
      SerialNo: this.selectedRow.selectedSerialNo
    };

    this.jobService.CheckPrevJob(payload).subscribe({
      next: (res) => {
        console.log("Job check result:", res);

        // ✅ Only open wizard if backend allows
        if (res.allow) {
          this.showWizard = true;
          this.wizardStep = 1;
          this.scannedData = null;
          this.stepValid = false;
          this.useCamera = false; 
        } else {
          Swal.fire({
            icon: 'warning',
            title: 'Cannot Start',
            text: res.message || "This job cannot be started yet.",
          });
        }
      },
      error: (err) => {
        console.error("Error checking job:", err);

        // Show backend message if available
        Swal.fire({
          icon: 'error',
          title: 'Job Start Error',
          text: err.error?.message || "Something went wrong while checking job start.",
        });
      }
    });

    

  }


     onCamerasFound(devices: MediaDeviceInfo[]) {
        this.availableDevices = devices || [];
        if (!this.selectedDevice && this.availableDevices.length) {
          this.selectedDevice = this.availableDevices[0];
        }
      }

      handleScannedData(raw: string) {
        try {
          this.scannedData = JSON.parse(raw);
        } catch {
          this.scannedData = this.parseQrData(raw);
        }
        console.log('Decoded QR:', this.scannedData);
        this.validateStep();
      }

      parseQrData(raw: string): any {
        const obj: any = {};
        raw.split(/\r?\n/).map(l => l.trim()).filter(Boolean).forEach(line => {
          const [k, ...rest] = line.split(':');
          if (k && rest.length) obj[k.trim()] = rest.join(':').trim();
        });
        return obj;
      }

      validateStep() {
        const q = (v: any) => (typeof v === 'string' ? v.toUpperCase() : v);

        switch (this.wizardStep) {
          case 1: // Job
            this.stepValid = q(this.scannedData?.qrType) === 'JOB' && this.scannedData?.job == this.job;
            if (this.stepValid) this.scannedSummary.job = this.scannedData.job;
            break;

          case 2: // Operation
            this.stepValid = q(this.scannedData?.qrType) === 'OPERATION' && this.scannedData?.operNum == this.operation;
            if (this.stepValid) this.scannedSummary.operation = this.scannedData.operNum;
            break;

          case 3: // Machine
            if (q(this.scannedData?.qrType) === 'MACHINE' && this.selectedRow) {
              const allMachines = this.selectedRow.employees?.flatMap((e: any) => e.machines.map((m: any) => m.machine_Num));
              this.stepValid = allMachines?.includes(this.scannedData?.machineNumber);
              if (this.stepValid) this.scannedSummary.machine = this.scannedData.machineNumber;
            } else {
              this.stepValid = false;
            }
            break;

          case 4: // Employee
            if (q(this.scannedData?.qrType) === 'EMPLOYEE' && this.selectedRow) {              
              const allEmployees = this.selectedRow.employees?.map((e: any) => e.empNum);
              this.stepValid = allEmployees?.includes(this.scannedData?.empNum);
              if (this.stepValid) this.scannedSummary.employee = this.scannedData.empNum;
            } else {
              this.stepValid = false;
            }
            break;
        }
      }



      async onFileSelected(event: any) {
        const file = event.target.files?.[0];
        if (!file) return;

        const imageUrl = URL.createObjectURL(file);

        try {
          const result = await this.qrReader.decodeFromImageUrl(imageUrl);

          // Always send raw text to handleScannedData
          const decodedText = result.getText();
          this.handleScannedData(decodedText);

        } catch (err: any) {
          console.error('QR decode failed:', err);

          // Decode failure (scanner couldn’t read image)
          Swal.fire('Error', 'Unable to read QR code from image', 'error');
        }
      }



    getStepMessage(): string {
        switch (this.wizardStep) {
          case 1: return this.stepValid ? 'Job QR matched' : ' Job mismatch';
          case 2: return this.stepValid ? 'Operation QR matched' : ' Operation mismatch';
          case 3: return this.stepValid ? 'Machine QR matched ' : ' Machine mismatch';
          case 4: return this.stepValid ? 'Employee QR matched' : ' Employee mismatch';
          default: return '';
        }
      }


      prevStep(): void {
        if (this.wizardStep > 1) {
          this.wizardStep--;
          this.scannedData = null;
          this.stepValid = false;
          this.useCamera = false;
        }
      }


      nextStep(): void {
        if (!this.stepValid) {
          Swal.fire('Error', 'Please scan the correct QR to proceed.', 'error');
          return;
        }
        if (this.wizardStep < 4) {
          this.wizardStep++;
          this.scannedData = null;
          this.stepValid = false;
          this.useCamera = false;
        } else {
          this.finishWizard();
        }
      }


  
      resumeJob(transNum: string, serialIndex: number) {
          const matchedJob = this.jobDetails.find(j => j.transNum === transNum);
          const job = matchedJob?.job || "N/A";
          const operation = matchedJob?.operNum?.toString() || "N/A";
          const machine = matchedJob?.['machineCode_' + serialIndex] || "N/A";
          const employee = matchedJob?.['empCode_' + serialIndex] || "N/A";
          const workCenter = matchedJob?.workCenter || "N/A";
          const serialNo = `${transNum}_${serialIndex}`;
          const startTime = this.jobService.getLocalDateTime();

          const payload = {
            job,
            operation,
            machine,
            employee,
            transNum: transNum.toString(),
            workCenter,
            serialNo,
            startTime
          };

          console.log("ResumeJob Payload:", payload);

          this.jobService.startJob(payload).subscribe({
            next: (res) => {
              console.log("ResumeJob API Response:", res);
              if (matchedJob) {
                matchedJob[`status_${serialIndex}`] = "Started";
                matchedJob[`statusTime_${serialIndex}`] = startTime;
              }
              this.startTimer(serialNo, startTime);
              Swal.fire("Resumed", "Job resumed successfully", "success");
            },
            error: (err) => {
              console.error("ResumeJob API Failed:", err);
              Swal.fire("Error", "Failed to resume job.", "error");
            }
          });
        }


  finishWizard() {
    console.log('Wizard finished', this.scannedSummary);

    // Attach the matched job object
    const matchedJob = this.selectedRow; 
    const job = this.scannedSummary.job;
    const operation = this.scannedSummary.operation;
    const machine = this.scannedSummary.machine;
    const employee = this.scannedSummary.employee;
    const transNum = matchedJob?.transNum?.toString() || "N/A";  
    const workCenter = matchedJob?.workCenter || 'N/A';    
    const serialNo = matchedJob?.selectedSerialNo || 'N/A';
    const startTime = this.jobService.getLocalDateTime();  

    // Build API payload
    const payload = {
      job,
      operation,
      machine,
      employee,
      transNum,
      workCenter,
      serialNo,
      startTime
    };

    console.log('Payload to send:', payload);

    // Show summary before API call
    Swal.fire({
      icon: 'success',
      title: 'Job Started Successfully',
      html: `
        <p><strong>Job:</strong> ${job}</p>
        <p><strong>Operation:</strong> ${operation}</p>
        <p><strong>Machine:</strong> ${machine}</p>
        <p><strong>Employee:</strong> ${employee}</p>
        <p><strong>Transaction No:</strong> ${transNum}</p>        
        <p><strong>Work Center:</strong> ${workCenter}</p>
        <p><strong>Serial No:</strong> ${serialNo}</p>
        <p><strong>Start Time:</strong> ${startTime}</p>
      `,
      width: 700
    });

    this.jobService.startJob(payload).subscribe({
      next: (res) => {
        console.log('StartJob API Response:', res);
        const rowKey = serialNo;

        // Update the status so button switches
        if (matchedJob) {
          matchedJob['status_' + serialNo.split('_').pop()] = 'Started';
        }

        // preserve old elapsed time if exists
        const baseMs = this.elapsedTimes[rowKey]
          ? this.timeStringToMs(this.elapsedTimes[rowKey])
          : 0;

        this.startTimer(rowKey, startTime, baseMs);
      },
      error: (err) => {
        console.error('StartJob API Failed:', err);
        Swal.fire('Error', 'Failed to start job. Please try again.', 'error');
      }
    });

    // Reset wizard
    this.showWizard = false;
    this.wizardStep = 1;
    this.scannedSummary = { job: null, operation: null, machine: null, employee: null };
  
  }

    private timeStringToMs(time: string): number {
      const [hh, mm, ss] = time.split(':').map(Number);
      return ((hh * 3600) + (mm * 60) + ss) * 1000;
    }


    pauseJob(transNum: string, serialIndex: number) {
      const transNumStr = transNum.toString();
      const matchedJob = this.jobDetails.find(j => j.transNum === transNum);
      const job = matchedJob?.job || "N/A";
      const operation = matchedJob?.operNum?.toString() || "N/A";
      const machine = matchedJob?.['machineCode_' + serialIndex] || "N/A";
      const employee = matchedJob?.['empCode_' + serialIndex] || "N/A";
      const workCenter = matchedJob?.workCenter || "N/A";
      const serialNo = `${transNum}_${serialIndex}`;
      const pauseTime = this.jobService.getLocalDateTime();

      const payload = {
        job,
        operation,
        machine,
        employee,
        transNum:transNumStr,
        workCenter,
        serialNo,
        startTime:pauseTime
      };

      console.log("PauseJob Payload:", payload);

      this.jobService.pauseJob(payload).subscribe({
        next: (res) => {
          console.log("PauseJob API Response:", res);
          this.stopTimer(serialNo);
          const row = this.jobDetails.find(j => j.transNum === transNum);
          if (row) {
            row[`status_${serialIndex}`] = "Paused";
            row[`statusTime_${serialIndex}`] = pauseTime;
          }

          Swal.fire({
            icon: 'warning',
            title: 'Job Paused',
            html: `
              <p><strong>Job:</strong> ${job}</p>
              <p><strong>Operation:</strong> ${operation}</p>
              <p><strong>Machine:</strong> ${machine}</p>
              <p><strong>Employee:</strong> ${employee}</p>
              <p><strong>Transaction No:</strong> ${transNumStr}</p>        
              <p><strong>Work Center:</strong> ${workCenter}</p>
              <p><strong>Serial No:</strong> ${serialNo}</p>
              <p><strong>Pause Time:</strong> ${pauseTime}</p>
            `,
            width: 700
          });
        },
        error: (err) => {
          console.error("PauseJob API Failed:", err);
          Swal.fire("Error", "Failed to pause job. Please try again.", "error");
        }
      });
    }

    completeJob(transNum: string, serialIndex: number) {
      const transNumStr = transNum.toString();
      const matchedJob = this.jobDetails.find(j => j.transNum === transNum);
      const job = matchedJob?.job || "N/A";
      const operation = matchedJob?.operNum?.toString() || "N/A";
      const machine = matchedJob?.['machineCode_' + serialIndex] || "N/A";
      const employee = matchedJob?.['empCode_' + serialIndex] || "N/A";
      const workCenter = matchedJob?.workCenter || "N/A";
      const serialNo = `${transNum}_${serialIndex}`;
      const endTime = this.jobService.getLocalDateTime();

      const payload = {
        job,
        operation,
        machine,
        employee,
        transNum:transNumStr,
        workCenter,
        serialNo,
        startTime:endTime
      };
      

      console.log("StopJob Payload:", payload);
      const row = this.jobDetails.find(j => j.transNum === transNum);
      if (row) {
        row[`status_${serialIndex}`] = "Completed";
        row[`statusTime_${serialIndex}`] = endTime;
      }

      this.jobService.completeJob(payload).subscribe({
        next: (res) => {
          console.log("StopJob API Response:", res);
          this.stopTimer(serialNo);
          Swal.fire({
            icon: 'success',
            title: 'Job Completed',
            html: `
              <p><strong>Job:</strong> ${job}</p>
              <p><strong>Operation:</strong> ${operation}</p>
              <p><strong>Machine:</strong> ${machine}</p>
              <p><strong>Employee:</strong> ${employee}</p>
              <p><strong>Transaction No:</strong> ${transNum}</p>        
              <p><strong>Work Center:</strong> ${workCenter}</p>
              <p><strong>Serial No:</strong> ${serialNo}</p>
              <p><strong>End Time:</strong> ${endTime}</p>
            `,
            width: 700
          });
        },
        error: (err) => {
          console.error("StopJob API Failed:", err);
          Swal.fire("Error", "Failed to stop job. Please try again.", "error");
        }
      });
    }


    
    
    

 startTimer(key: string, startTime: string, baseMs: number = 0) {
  const start = new Date(startTime).getTime();

  const tick = () => {
    const now = Date.now();
    const diff = baseMs + (now - start);
    this.elapsedTimes[key] = this.formatTime(diff);
    this.timerIntervals[key] = requestAnimationFrame(tick);
  };

  this.stopTimer(key); // stop old timer before starting new
  this.timerIntervals[key] = requestAnimationFrame(tick);
}



    stopTimer(key: string) {
      if (this.timerIntervals[key]) {
        cancelAnimationFrame(this.timerIntervals[key]);
        delete this.timerIntervals[key];
      }
    }

 

  formatTime(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${hours.toString().padStart(2, '0')}:` +
         `${minutes.toString().padStart(2, '0')}:` +
         `${seconds.toString().padStart(2, '0')}`;
}


  

getRowColor(status: string): string {
  switch (status) {
    case 'Started':
      return '#cce5ff';   // blue
    case 'Paused':
      return '#fff3cd';   // yellow
    case 'Completed':
      return '#d4edda';   // green
    default:
      return '#fffeccff'; // light gray
  }
}

  getEmployeeList(): string {
    if (!this.selectedRow || !this.selectedRow.employees) {
      return '';
    }
    return this.selectedRow.employees.map((e: { empNum: any; }) => e.empNum).join(', ');
  }

  getMachineList(): string {
    if (!this.selectedRow || !this.selectedRow.employees) {
      return '';
    }

    // Collect all machines from all employees
    const machines: string[] = [];
    this.selectedRow.employees.forEach((emp: any) => {
      if (emp.machines && emp.machines.length > 0) {
        emp.machines.forEach((m: any) => {
          machines.push(`${m.machine_Num}`);
        });
      }
    });

    return machines.join(', ');
  }






  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }
}
