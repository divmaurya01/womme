import { Component, OnInit, ViewChild, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule, Table } from 'primeng/table';
import { ZXingScannerModule } from '@zxing/ngx-scanner';
import { DialogModule } from 'primeng/dialog';
import Swal from 'sweetalert2';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { LoaderService } from '../../services/loader.service';
import { finalize, flatMap } from 'rxjs/operators';
import { BrowserQRCodeReader } from '@zxing/browser';

@Component({
  selector: 'app-unposted-job-transaction',
  templateUrl: './unposted-job-transaction.html',
  styleUrls: ['./unposted-job-transaction.scss'],
  standalone: true,
  imports: [
    CommonModule,
    HeaderComponent,
    SidenavComponent,
    TableModule,
    FormsModule,
    DialogModule,
    ZXingScannerModule
  ]
})
export class UnpostedJobTransaction implements OnInit {
  @ViewChild('dt') dt!: Table;
  dtTrigger: Subject<any> = new Subject();

  employeeCode:string = '';
  role_id:number = 0;
  transactions: any[] = [];
  matchedData: any = {};
  totalRecords: number = 0;
  page: number = 0;
  size: number = 50;
  searchTerm: string = '';
  isLoading: boolean = false;
  isSidebarHidden = false;

  activeJobTrans: any[] = [];
  showWizard: boolean = false;
  wizardStep: number = 1;
  useCamera: boolean = false;

  scannedData: string | null = null;
  stepValid: boolean = false;

  nextJobActiveMap = new Map<string, boolean>();

  selectedJob: any = null;
  qrReader = new BrowserQRCodeReader();
  availableMachines: string[] = [];  // fill from API when needed
  availableEmployees: string[] = []; // fill from API when needed

  activeTimers: { [key: string]: any } = {};
  // Scanner-specific
  availableDevices: MediaDeviceInfo[] = [];
  selectedDevice: MediaDeviceInfo | null = null;

  constructor(private jobService: JobService,
              private router: Router,
              private route: ActivatedRoute,
              private loader: LoaderService,
              private zone: NgZone) {}

  ngOnInit(): void {
    this.loadJobs();
    
  }

  ngOnDestroy() {
    Object.values(this.activeTimers).forEach(timer => clearInterval(timer));
  }


  loadJobs(pageEvent?: any) {
    this.isLoading = true;
    this.loader.show();

    const page = pageEvent?.first ? pageEvent.first / pageEvent.rows : this.page;
    const size = pageEvent?.rows ?? this.size;

    const userDetails = JSON.parse(localStorage.getItem('userDetails') || '{}');
    this.employeeCode = userDetails?.employeeCode;
    this.role_id = userDetails.roleID;

    // Load unposted jobs
    this.jobService.GetUnpostedTransactions(page, size, this.searchTerm, this.employeeCode)
      .subscribe({
        next: (jobRes: any) => {
          const rawJobs = jobRes.data ?? [];

          // Load next-job-active info
          this.jobService.getIsNextJobActive().subscribe({
            next: (nextRes: any) => {

              // --------------------------------------
              // Build job|serial → next operations map
              // --------------------------------------
              const nextOpMap = new Map<string, number[]>();

              (nextRes.data ?? []).forEach((x: any) => {
                const key = `${x.job}|${x.serialNo}`;
                if (!nextOpMap.has(key)) nextOpMap.set(key, []);
                nextOpMap.get(key)!.push(Number(x.nextOper));
              });

              // --------------------------------------
              // FINAL FILTER + MAP (ONCE)
              // --------------------------------------
              let filteredJobs = rawJobs.map((x: any) => ({
                serialNo: (x.serialNo ?? '').toString().trim(),
                jobNumber: (x.job ?? '').toString().trim(),
                qtyReleased: x.qtyReleased,
                operationNumber: x.operNum ?? x.operationNumber,
                wcCode: (x.wcCode ?? '').toString().trim(),
                wcDescription: (x.wcDescription ?? '').toString().trim(),
                emp_num: '',
                machine_id: '',
                a_hrs: 0
              }));

              // Apply next-job filter ONLY for role 4
              if (this.role_id === 4) {
                filteredJobs = filteredJobs.filter((job: any) => {
                  const key = `${job.jobNumber}|${job.serialNo}`;
                  const nextOps = nextOpMap.get(key) ?? [];
                  return nextOps.includes(Number(job.operationNumber));
                });

              }

              // SINGLE ASSIGNMENT
              this.transactions = filteredJobs;
              this.totalRecords = filteredJobs.length;

              // Load running transactions AFTER final list
              this.loadActiveJobTransactions();

              this.isLoading = false;
              this.loader.hide();

              console.log('Final jobs shown:', this.transactions.length);
            },
            error: err => this.handleError(err)
          });
        },
        error: err => this.handleError(err)
      });
  }

  private handleError(err: any) {
    console.error('API Error:', err);
  
    this.isLoading = false;
    this.loader.hide();
  
    Swal.fire({
      icon: 'error',
      title: 'Error',
      text: err?.error?.message || 'Something went wrong. Please try again.',
    });
  }
  

  onSearchChange(value: string) {
    this.searchTerm = value;
    this.loadJobs({ first: 0, rows: this.size });
  }

  startJobWizard(job: any) {
    this.jobService.canStartJob(job.jobNumber, job.serialNo, job.operationNumber).subscribe({
      next: (res) => {
        if (res.canStart) {
          
          this.selectedJob = job;
          this.wizardStep = 1;
          this.scannedData = null;
          this.stepValid = false;
          this.showWizard = true;

          console.log('Selected row data:', job);

          this.fetchEmployees();
          this.fetchMachines();
        } else {
          Swal.fire('Blocked', res.message || 'This job cannot be started right now.', 'warning');
        }
      },
      error: (err) => {
        console.error('Error calling canStartJob:', err);
        Swal.fire('Error', 'Unable to verify job start condition', 'error');
      }
    });
  }


  





  fetchEmployees() {
    if (!this.selectedJob) return;
    const jobNumber = this.selectedJob.jobNumber;
    const operationNumber = this.selectedJob.operationNumber;

    this.jobService.getEmployeesForJob(jobNumber, operationNumber)
      .subscribe({
        next: (res: any) => {
          // Map to empNum strings
          this.availableEmployees = (res ?? []).map((e: any) => e.empNum);
        },
        error: (err) => {
          console.error('Error fetching employees:', err);
        }
      });
  }

  fetchMachines() {
    if (!this.selectedJob) return;
    const jobNumber = this.selectedJob.jobNumber;
    const operationNumber = this.selectedJob.operationNumber;

    this.jobService.getMachinesForJob(jobNumber, operationNumber)
      .subscribe({
        next: (res: any) => {
          // Map to machineId strings
          this.availableMachines = (res ?? []).map((m: any) => m.machineId);
        },
        error: (err) => {
          console.error('Error fetching machines:', err);
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

  
      async onFileSelected(event: any) {
          const file = event.target.files?.[0];
          if (!file) return;
  
          const imageUrl = URL.createObjectURL(file);
  
          try {
            const result = await this.qrReader.decodeFromImageUrl(imageUrl);
            this.handleScannedData(result.getText());
          } catch (err: any) {
            this.showWizard = false;
            console.error('QR decode failed:', err);
            Swal.fire('Error', 'Unable to read QR code from image', 'error');
          }
        }
  

  validateStep() {
    this.stepValid = false;

    if (!this.scannedData || !this.selectedJob) return;

    let parsed: any;

    if (typeof this.scannedData === 'string') {
      try {
        parsed = JSON.parse(this.scannedData);
      } catch (e) {
        console.error('Invalid QR data', e);
        return;
      }
    } else {
      parsed = this.scannedData;
    }

    switch (this.wizardStep) {
      case 1:
        this.stepValid = parsed.job === this.selectedJob.jobNumber;
        if (this.stepValid) {
          this.matchedData.jobNumber = parsed.job;
          this.matchedData.serialNo = this.selectedJob.serialNo;
          this.matchedData.wc = this.selectedJob.wcCode;
          this.matchedData.qtyReleased = this.selectedJob.qtyReleased;         
          this.matchedData.loginuser = this.employeeCode;
        }
        break;

      case 2:
        this.stepValid = parsed.operNum === this.selectedJob.operationNumber;
        if (this.stepValid) {
          this.matchedData.operationNumber = parsed.operNum;
        }
        break;

      case 3:
        this.stepValid = this.availableMachines.includes(parsed.machineNumber);

        if (this.stepValid) {
          this.matchedData.machineNumber = parsed.machineNumber;

          // If role_id is 4, auto-fill empNum as current employee
          if (this.role_id === 4) {
            parsed.empNum = this.employeeCode;
            this.scannedData = parsed;
            this.matchedData.empNum = this.employeeCode;
          }
        }
        break;

      case 4:
        this.stepValid = this.availableEmployees.includes(parsed.empNum);
        if (this.stepValid) {
          this.matchedData.empNum = parsed.empNum;
        }
        break;
    }
  }





  nextStep() {
    if (!this.stepValid) return;

    if (this.wizardStep === 3 && this.role_id === 4) {
      
      this.finishWizard();
    } else {
      this.wizardStep++;
      this.scannedData = null;
      this.stepValid = false;
    }
  }


  prevStep() {
    if (this.wizardStep > 1) {
      this.wizardStep--;
      this.scannedData = null;
      this.stepValid = false;
    }
  }

  finishWizard() {
    console.log('Matched Data:', this.matchedData);

      this.jobService.startJob(this.matchedData).subscribe({
      next: (res: any) => {
       Swal.fire({
        icon: 'success',
        title: 'Success',
        text: 'Job has been started successfully!',
        showConfirmButton: false,
        timer: 3000
      });
    this.searchTerm = '';  
    this.loadJobs();
        this.loadActiveJobTransactions();
        this.showWizard = false;
        this.matchedData = {}; // reset after success
      },
      error: (err) => {
        console.error('Error starting job:', err);
        Swal.fire('Error', 'Failed to start job', 'error');
      }
    });
  }

  PauseJob(selectedRow: any) {
    console.log('Selected Row Data:', selectedRow);

    // Build payload for API
    const payload = {
      jobNumber: selectedRow.jobNumber,
      serialNo: selectedRow.serialNo,
      wc: selectedRow.wcCode,
      operationNumber: Number(selectedRow.operationNumber),
      machineNumber: selectedRow.machine_id, // use actual machine_id
      empNum: selectedRow.emp_num,         // use actual emp_num
      qtyReleased: selectedRow.qtyReleased,
      loginuser: this.employeeCode // logged-in user
    };

    console.log(payload);
    this.jobService.PauseJob(payload).subscribe({
      next: (res: any) => {
        // Stop timer for this job
        if (this.activeTimers[selectedRow.serialNo]) {
          clearInterval(this.activeTimers[selectedRow.serialNo]);
          delete this.activeTimers[selectedRow.serialNo];
        }

                  Swal.fire({
                      icon: 'success',
                      title: 'Success',
                      text: 'Job has been paused successfully!',
                      showConfirmButton: false,
                      timer: 3000
                    });

          this.searchTerm = '';  
          this.loadJobs();
        // Reload active job transactions
        this.loadActiveJobTransactions();
      },
      error: (err) => {
        console.error('Error pausing job:', err);
        Swal.fire('Error', 'Failed to pause job', 'error');
      }
    });
  }


  CompleteJob(selectedRow: any) {
    console.log('Selected Row Data:', selectedRow);

    // Build payload for API
    const payload = {
      jobNumber: selectedRow.jobNumber,
      serialNo: selectedRow.serialNo,
      wc: selectedRow.wcCode,
      operationNumber: Number(selectedRow.operationNumber),
      machineNumber: selectedRow.machine_id, // use actual machine_id
      empNum: selectedRow.emp_num,         // use actual emp_num
      qtyReleased: selectedRow.qtyReleased,
      loginuser: this.employeeCode // or whichever user is logged in
    };

    this.jobService.CompleteJob(payload).subscribe({
      next: (res: any) => {
                Swal.fire({
              icon: 'success',
              title: 'Success',
              text: 'Job has been completed successfully!',
              showConfirmButton: false,
              timer: 3000
            });

        clearInterval(this.activeTimers[selectedRow.serialNo]);
        this.searchTerm = '';  
    this.loadJobs();
        this.loadActiveJobTransactions();
      },
      error: (err) => {
        console.error('Error pausing job:', err);
        Swal.fire('Error', 'Failed to pause job', 'error');
      }
    });
  }


  loadActiveJobTransactions() {
    this.jobService.GetActiveJobTransactions().subscribe({
      next: (res: any) => {
        this.activeJobTrans = res?.data ?? [];
        this.mapTransToJobs();
      },
      error: err => console.error('Error fetching active job transactions:', err)
    });
  }

  mapTransToJobs() {
    if (!this.transactions?.length || !this.activeJobTrans?.length) return;

    this.transactions = this.transactions.map(job => {
      const match = this.activeJobTrans.find(tr =>
        (tr.job ?? '').toString().trim() === job.jobNumber &&
        Number(tr.oper_num) === Number(job.operationNumber) &&
        (tr.serialNo ?? tr.serial_no ?? tr.SerialNo ?? '').toString().trim() === job.serialNo
      );

      if (match) {
        const key = job.serialNo;

        // Convert backend hours to seconds
        const accumulated = Math.floor((match.total_a_hrs ?? 0) * 3600);

        let elapsedSeconds = accumulated;

        // Only start timer if job is running
        if (match.status === "1" && match.start_time) {
          const startTime = new Date(match.start_time);
          elapsedSeconds += Math.floor((new Date().getTime() - startTime.getTime()) / 1000);

          if (this.activeTimers[key]) clearInterval(this.activeTimers[key]);

          this.activeTimers[key] = setInterval(() => {
            const target = this.transactions.find(x => x.serialNo === key);
            if (target) target.elapsedSeconds += 1;
          }, 1000);
        } else {
          // paused or completed
          if (this.activeTimers[key]) {
            clearInterval(this.activeTimers[key]);
            delete this.activeTimers[key];
          }
        }

        return {
          ...job,
          emp_num: match.emp_num ?? '',
          machine_id: match.machine_id ?? '',
          a_hrs: accumulated,
          status: match.status,
          elapsedSeconds
        };
      }

      return job;
    });
  }



  formatTime(seconds: number): string {
    if (!seconds && seconds !== 0) return '00:00:00';
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);
    return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }





  getStepMessage(): string {
    if (this.stepValid) return 'Matched successfully';
    return 'Scanned data does not match';
  }

  getMachineList() {
    return this.availableMachines.join(', ');
  }

  getEmployeeList() {
    return this.availableEmployees.join(', ');
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }
}
