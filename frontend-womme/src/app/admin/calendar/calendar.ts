import { Component, OnInit, ChangeDetectorRef, HostListener } from '@angular/core';
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
import * as XLSX from 'xlsx';

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
    SidenavComponent, DialogModule, TableModule, ButtonModule
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
 isSidebarHidden = window.innerWidth <= 1024;
  markedDates: { [key: string]: number } = {}; // YYYY-MM-DD -> flag
  showCalendar = false;
  showCalendarDialog = false;
  submitted = false;
  monthList = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
  ];

  yearList: number[] = [];
  selectedYear = new Date().getFullYear();

  showMonthCalendar = false;
  monthViewDate: Date = new Date();
  calendarDialog: boolean = false;
  showCalendarScreen: boolean = false;

  allCalendars: any[] = [];      // full data from API
  filteredCalendars: any[] = []; // table binding
  globalSearch = '';


  selectedMonth: number = 0;
  openCalendarDialog: boolean = false; // controls dialog visibility

  dummyDate: Date = new Date();



  constructor(private jobService: JobService, private cdr: ChangeDetectorRef, private loader: LoaderService) { }

  ngOnInit() {
    this.checkScreenSize();
    this.loadCalendarList();
    this.fetchMarkedDates();
    for (let y = 2000; y <= 2050; y++) {
      this.yearList.push(y);
    }
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
  loadCalendarList() {
    this.loader.show();
    this.jobService.GetAllCalendars()
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: (res: any[]) => {
          const list = (Array.isArray(res) ? res : []).map(entry => {
            const d = new Date(entry.date);
            return {
              ...entry,
              date: `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`,
              typeText: entry.flag === 0 ? 'Overtime' : 'Double Time'
            };
          });

          this.allCalendars = list;            // ✅ full data
          this.filteredCalendars = [...list]; // ✅ table data

          this.fetchMarkedDates();
        },
        error: err => {
          console.error("Failed to load calendars:", err);
          this.allCalendars = [];
          this.filteredCalendars = [];
        }
      });
  }

onGlobalSearch(value: string): void {
  const rawSearch = value.toLowerCase().trim();

  if (!rawSearch) {
    this.filteredCalendars = [...this.allCalendars];
    return;
  }

  const keywords = rawSearch
    .split('|')
    .map(k => k.trim())
    .filter(k => k.length > 0);

  this.filteredCalendars = this.allCalendars.filter(item =>
    keywords.some(keyword =>
      Object.values(item).some(val =>
        val?.toString().toLowerCase().includes(keyword)
      )
    )
  );
}



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
                  const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
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

  onExcelUpload(event: any) {
    const file = event.target.files[0];
    if (!file) return;

    const reader = new FileReader();

    reader.onload = (e: any) => {
      const workbook = XLSX.read(e.target.result, { type: 'binary' });
      const sheetName = workbook.SheetNames[0];
      const sheet = workbook.Sheets[sheetName];

      const data = XLSX.utils.sheet_to_json(sheet);

      // Transform excel rows to API payload
      const payload = data.map((row: any) => ({
        date: this.formatExcelDate(row.date),
        flag: Number(row.flag),
        calendarDescription: row.calendarDescription,
        occasion: row.occasion
      }));

      this.uploadCalendarData(payload);
    };

    reader.readAsBinaryString(file);
  }

  formatExcelDate(value: any): string {
    if (value instanceof Date) {
      return value.toISOString().split('T')[0];
    }

    if (typeof value === 'number') {
      const date = XLSX.SSF.parse_date_code(value);
      return `${date.y}-${String(date.m).padStart(2, '0')}-${String(date.d).padStart(2, '0')}`;
    }

    return value; // already yyyy-mm-dd
  }

  uploadCalendarData(payload: any[]) {
    this.loader.show();

    this.jobService.importCalendar(payload)   // <-- send ARRAY directly
      .pipe(finalize(() => this.loader.hide()))
      .subscribe({
        next: () => {
          Swal.fire('Success', 'Calendar imported successfully!', 'success');
          this.loadCalendarList();
          this.fetchMarkedDates();
        },
        error: err => {
          console.error(err);
          Swal.fire('Error', 'Failed to import calendar', 'error');
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


  isDateAlreadyUsed(): boolean {
    if (!this.selectedDateStr) return false;

    return this.calendarList.some(entry => entry.date === this.selectedDateStr);
  }

  toggleSidebar() {
    this.isSidebarHidden = !this.isSidebarHidden;
  }

  getHoverText(date: any): string {
    const dateStr = `${date.year}-${String(date.month + 1).padStart(2, '0')}-${String(date.day).padStart(2, '0')}`;

    const entry = this.allCalendars.find(x => x.date === dateStr);

    if (!entry) return '';

    return entry.flag === 0 ? 'Overtime' : 'Double Time';
  }


  getHoverTextFromString(dateStr: string): string {
    const entry = this.allCalendars.find(x => x.date === dateStr);
    if (!entry) return '';
    return entry.flag === 0 ? 'Overtime' : 'Double Time';
  }





  backToMonths() {
    this.showCalendarScreen = false;
  }



  getDateClass(date: any): string {

    // PrimeNG month is ZERO-based — DO NOT modify
    const fullDate = `${date.year}-${String(date.month + 1).padStart(2, '0')}-${String(date.day).padStart(2, '0')}`;

    // Sunday → always overtime
    const jsDate = new Date(date.year, date.month, date.day);
    if (jsDate.getDay() === 0) {
      return 'overtime-day';
    }

    // 🔥 USE allCalendars (not calendarList)
    const entry = this.allCalendars.find(x => x.date === fullDate);
    if (!entry) return '';

    return entry.flag === 0 ? 'overtime-day' : 'doubletime-day';
  }


  openCalendarForDate(dateStr: string, event: Event) {
    event.preventDefault();

    const parts = dateStr.split("-");
    this.selectedYear = Number(parts[0]);
    this.selectedMonth = Number(parts[1]);

    this.monthViewDate = new Date(this.selectedYear, this.selectedMonth - 1, 1);

    this.calendarDialog = true;
    this.showCalendarScreen = true;
  }
  loadYearCalendar() {
    if (this.selectedMonth > 0) {
      this.openCalendarMonth(this.selectedMonth);
    }
  }
  openShowCalendarDialog() {
    this.openCalendarDialog = true;
    this.showCalendarScreen = false; // show months initially
  }
  openCalendarMonth(month: number) {
    this.selectedMonth = month;
    this.monthViewDate = new Date(this.selectedYear, month - 1, 1);
    this.dummyDate = new Date(this.selectedYear, month - 1, 1);
    this.showCalendarScreen = true;
  }

  exportCalendarToExcel(): void {
  if (!this.filteredCalendars || this.filteredCalendars.length === 0) {
    Swal.fire('No Data', 'No Calendar data available to export', 'info');
    return;
  }

  const exportData = this.filteredCalendars.map((cal, index) => ({
    'Sr No': index + 1,
    'Date': cal.date,
    'Occasion': cal.occasion,
    'Description': cal.calendarDescription,
    'Type': cal.typeText   // Overtime / Double Time
  }));

  const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(exportData);
  const workbook: XLSX.WorkBook = {
    Sheets: { 'Calendar': worksheet },
    SheetNames: ['Calendar']
  };

  XLSX.writeFile(workbook, 'Calendar_List.xlsx');
}


  
}