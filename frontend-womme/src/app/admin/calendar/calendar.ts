import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { JobService } from '../../services/job.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CalendarModule } from 'primeng/calendar';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import Swal from 'sweetalert2';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
@Component({
  selector: 'app-calendar-page',
  templateUrl: './calendar.html',
  styleUrls: ['./calendar.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    CalendarModule,
    HeaderComponent,
    SidenavComponent,DialogModule,TableModule,ButtonModule
  ],
})
export class CalendarComponent implements OnInit {
  selectedDate: Date | null = null;
  selectedDateStr: string = '';
  occasion = '';
  calendarDescription = '';
  selectedType: number = 0;
  calendarList: any[] = [];
  successMessage = '';
  isSidebarHidden = false;
  markedDates: { [key: string]: number } = {}; // YYYY-MM-DD -> flag
  showCalendar = false;
  showCalendarDialog = false;
  submitted = false;

  constructor(private jobService: JobService, private cdr: ChangeDetectorRef,private loader:LoaderService) {}

  ngOnInit() {
    this.loadCalendarList();
    this.fetchMarkedDates();
  }

  loadCalendarList() {
    this.loader.show();
    this.jobService.GetAllCalendars()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any[]) => {
          this.calendarList = (Array.isArray(res) ? res : []).map(entry => {
            try {
              const d = new Date(entry?.date);
              if (isNaN(d.getTime())) throw new Error("Invalid date");

              return {
                ...entry,
                date: `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
              };
            } catch (err) {
              console.warn("Skipping invalid entry:", entry, err);
              return null;
            }
          }).filter(Boolean); // remove nulls

          this.fetchMarkedDates(); // fill markedDates
        },
        error: err => {
          console.error("Failed to load calendars:", err);
          this.calendarList = [];
        }
      });
  }
// showCalendarDialog = false;

// Open dialog
openAddDialog(form?: any) {
  this.resetForm(form);
    this.submitted = false;     
  this.showCalendarDialog = true;
    this.showCalendar = false;   // hide inline calendar initially
}

// Close dialog
closeDialog() {
  this.showCalendarDialog = false;
  this.submitted = false;      
  this.showCalendar = false;   // also hide calendar
}

toggleCalendar() {
  this.showCalendar = !this.showCalendar; // toggle immediately
  if (this.showCalendar) {
    this.fetchMarkedDates(); // fetch marked dates, no need to wait for toggle
  }
}



fetchMarkedDates(callback?: () => void) {
  this.loader.show();
  this.jobService.GetAllCalendars()
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (data: any[]) => {
        this.markedDates = {};
        if (Array.isArray(data)) {
          data.forEach(entry => {
            try {
              const d = new Date(entry?.date);
              if (!isNaN(d.getTime())) {
                const key = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
                this.markedDates[key] = entry?.flag ?? false;
              }
            } catch (err) {
              console.warn("Invalid entry skipped:", entry, err);
            }
          });
        }
        console.log("Loaded marked dates:", this.markedDates);
        this.cdr.detectChanges();
        callback?.();
      },
      error: err => console.error("Failed to fetch calendars:", err)
    });
}


  // Called from HTML dateTemplate ng-template
  isDateMarked(date: any): string {
    const dateStr = `${date.year}-${String(date.month).padStart(2, '0')}-${String(date.day).padStart(2, '0')}`;
    const flag = this.markedDates[dateStr];
    if (flag === 0) return 'overtime-day';
    if (flag === 1) return 'doubletime-day';
    return '';
  }


onDateSelect(date: Date) {
  this.selectedDate = date;

  // Format date as YYYY-MM-DD for input
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  this.selectedDateStr = `${year}-${month}-${day}`; // <-- update input

  this.showCalendar = false; // hide calendar after selection
}

// Optional: if user types manually in input
onInputChange(value: string) {
  // Only parse if value is a valid date
  const parsed = new Date(value);
  if (!isNaN(parsed.getTime())) {
    this.selectedDate = parsed;
  }
}
  showValidation = false;

  submitCalendar() {
    this.submitted = true; 

    if (!this.selectedDateStr || !this.occasion || !this.calendarDescription) {
      return; 
    }

    if (this.isDateAlreadyUsed()) {
      this.showCalendarDialog = false;
      setTimeout(() => {
        Swal.fire({
          icon: 'warning',
          title: 'Date Already Used',
          text: 'This date already exists. Please choose another date.'
        });
      }, 50);
      return;
    }

    const payload = {
      date: this.selectedDateStr,
      occasion: this.occasion,
      calendarDescription: this.calendarDescription,
      flag: this.selectedType
    };

    this.loader.show();
    this.jobService.addCalendar(payload)
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          this.submitted = false;
          Swal.fire({
            icon: 'success',
            title: 'Success',
            text: 'Date added successfully!',
            showConfirmButton: true
          });

          this.loadCalendarList();
          this.fetchMarkedDates();
          this.resetForm();
          this.showCalendarDialog = false;
        },
        error: (err) => {
          console.error("Add calendar failed:", err);
          Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'Failed to add date. Please try again.'
          });
        }
      });

  }


  deleteEntry(id: number, event: Event) {
    event.preventDefault();

    Swal.fire({
      icon: 'warning',
      title: 'Are you sure?',
      text: 'This entry will be deleted!',
      showCancelButton: true,
      confirmButtonText: 'Yes, delete it!',
      cancelButtonText: 'Cancel',
    }).then((result) => {
      if (result.isConfirmed) {
        this.loader.show();
        this.jobService.deleteCalendar(id)
          .pipe(finalize(() => this.loader.hide()))
          .subscribe({
            next: () => {
              Swal.fire({
                icon: 'success',
                title: 'Deleted!',
                text: 'Entry deleted successfully.',
                timer: 2000,
                showConfirmButton: false
              });
              this.loadCalendarList();
              this.fetchMarkedDates();
            },
            error: (err) => {
              console.error("Delete failed:", err);
              Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Failed to delete entry. Please try again.'
              });
            }
          });
      }
    });
  }

 resetForm(form?: any) {
  this.selectedDate = null;
  this.selectedDateStr = '';
  this.occasion = '';
  this.calendarDescription = '';
  this.selectedType = 0;

  if (form) {
    form.resetForm(); // reset the form if passed
  }
}

getDateClass(date: any): string {
  const dateStr = `${date.year}-${String(date.month + 1).padStart(2, '0')}-${String(date.day).padStart(2, '0')}`;
  const flag = this.markedDates[dateStr];

  console.log(`Date: ${dateStr}, Flag: ${flag}`); 

  if (flag === 0) return 'overtime-day';
  if (flag === 1) return 'doubletime-day';
  return '';
}

isDateAlreadyUsed(): boolean {
  if (!this.selectedDateStr) return false;

  return this.calendarList.some(entry => entry.date === this.selectedDateStr);
}

  toggleSidebar() {
    this.isSidebarHidden = !this.isSidebarHidden;
  }
}
