import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobService } from '../../services/job.service';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import Swal from 'sweetalert2';
import { TimeoutError } from 'rxjs';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-operation-masters',
  templateUrl: './operation.html',
  styleUrls: ['./operation.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, HeaderComponent, SidenavComponent, TableModule, DialogModule]
})
export class OperationMastersComponent implements OnInit {
  isSidebarHidden = false;
  showForm = false;
  operations: any[] = [];
  searchText = '';
  formError: string = '';
  totalOperations = 0;
  isOperationLoading = false;
  operationSearchTerm = '';
  form = {
    entryNo: 0,
    operationNumber: 0,
    operationName: '',
    operationDescription: '',
    updatedAt: ''
  };

  constructor(private jobService: JobService,private loader:LoaderService) {}

  ngOnInit(): void {
    this.loadOperations();
  }

  toggleSidebar(): void {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

 loadOperations(event?: any): void {
  this.isOperationLoading = true;
  const page = event?.first ? event.first / event?.rows : 0;
  const size = event?.rows || 50; 
  this.loader.show();
  this.jobService.GetDistinctOperations(page, size, this.operationSearchTerm)
  .pipe(finalize(() => this.loader.hide()))
  .subscribe({
    next: (res) => {
      this.operations = res.data.map((num: number, index: number) => ({
        entryNo: page * size + index + 1,
        operationNumber: num
      }));
      this.totalOperations = res.total; 
      this.isOperationLoading = false;
    },
    error: (err) => {
      this.isOperationLoading = false;
      Swal.fire('Error', 'Failed to load operations', 'error');
      console.error('Error loading operations:', err);
    }
  });
}

onOperationSearchChange(value: string): void {
  this.operationSearchTerm = value;
  this.loadOperations();
}




  openAddDialog(form?:any): void {
   
    this.showForm = true;
  }

  closeForm(): void {
    this.showForm = false;
  }

  downloadOperationQR(operation: string) {
    this.loader.show();
    this.jobService.downloadOperationQR(operation)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe((blob: Blob) => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `Operation-${operation }-QR.png`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    }, error => {
      console.error("Error downloading QR:", error);
    });
  }

  


allowOnlyNumbers(event: KeyboardEvent): void {
  const charCode = event.charCode;
  if (charCode < 48 || charCode > 57) {
    event.preventDefault();
  }
}

  

}
