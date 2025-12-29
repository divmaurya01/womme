import { CommonModule } from '@angular/common';
import { Component, Input, Output, EventEmitter, ViewEncapsulation, OnInit} from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faChartLine, faUserTie, faClipboardCheck, faUserCheck, faSyncAlt, faArrowUpRightDots, faCheckCircle, faClockRotateLeft, faCloudUploadAlt, faSpellCheck, faFileLines, faChartColumn, faDiagramProject, faNetworkWired, faIndustry, faUsers, faCogs, faBoxesStacked, faUserShield, faReceipt, faCalendarDays, faGear } from '@fortawesome/free-solid-svg-icons';
import {CdkDragDrop, moveItemInArray, DragDropModule} from '@angular/cdk/drag-drop';
import { JobService } from '../../services/job.service'; // use your existing JobService
import { ActivatedRoute } from '@angular/router'; // Import this
import { NgZone } from '@angular/core';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';
 
interface SidebarItem {
  label: string;
  link: string;
  icon: any;
  color?: string;
}
 
interface SidebarGroup {
  title: string;
  items: SidebarItem[];
}
 
@Component({
  selector: 'app-sidenav',
  templateUrl: './sidenav.html',
  styleUrls: ['./sidenav.scss'],
  standalone: true,
  imports: [CommonModule, RouterModule, FontAwesomeModule, DragDropModule],
  encapsulation: ViewEncapsulation.None,
})
export class SidenavComponent implements OnInit {
  @Input() isSidebarHidden: boolean = false;
  @Output() sectionSelected = new EventEmitter<string>();
 
  currentUrl: string = '';
  groupedSidebarItems: SidebarGroup[] = [];
 
    private readonly allSidebarItems: SidebarGroup[] = [
    {
      title: 'Operations',
      items: [
        // Dashboards
        { label: 'Main Dashboard', link: '/dashboard_admin', icon: faChartLine, color: '#E9A115' },
        { label: 'Employee Dashboard', link: '/dashboard_emp', icon: faUserTie, color: '#1976D2' },
        { label: 'QC Dashboard', link: '/dashboard_qc', icon: faClipboardCheck, color: '#70eaadff' },
        { label: 'Verify Dashboard', link: '/dashboard_verify', icon: faUserCheck, color: '#70eae6ff' },
        //{ label: 'Dashboard', link:'/dashboard', icon: faTachometerAlt, color: '#70eae6ff' }, 

        // Jobs & Transactions
        { label: 'Job Sync', link: '/jobs', icon: faSyncAlt, color: '#0097A7' },
        { label: 'Issue Transaction', link: '/issuejobtransaction', icon: faArrowUpRightDots, color: '#7B1FA2' },
        { label: 'Verify Transaction', link: '/verify-transaction', icon: faCheckCircle, color: '#388E3C' },
        { label: 'Unposted Transaction', link: '/unpostedjobtransaction', icon: faClockRotateLeft, color: '#F57C00' },
        { label: 'Posted Transaction', link: '/postedjobtransaction', icon: faCloudUploadAlt, color: '#43A047' },

        // Quality & Reports
        { label: 'Quality Check', link: '/qualitychecker', icon: faSpellCheck, color: '#F4A261' },
        { label: 'Main Reports', link: '/JobListComponent', icon: faFileLines, color: '#0288D1' },
        { label: 'Job Report', link: '/job-report', icon: faChartColumn, color: '#ff7300ff' },
      ],
    },

    {
      title: 'Manage',
      items: [
        // Mapping & Setup
        { label: 'WC / Machine Mapping', link: '/machine-employee', icon: faDiagramProject, color: '#F9A825' },
        { label: 'WC / Employee Mapping', link: '/workcenter', icon: faNetworkWired, color: '#7E57C2' },

        // Masters
        { label: 'Machines', link: '/machines', icon: faIndustry, color: '#6A1B9A' },
        { label: 'Employee', link: '/users', icon: faUsers, color: '#1E88E5' },
        { label: 'Operations', link: '/operations', icon: faCogs, color: '#E53935' },
        { label: 'Item Master', link: '/items', icon: faBoxesStacked, color: '#558B2F' },
        { label: 'Role Master', link: '/rolemaster', icon: faUserShield, color: '#FB8C00' },
        // { label: 'Assign Job', link: '/assignjob', icon: faIndustry, color: '#816CDA' },

        // Utilities
        { label: 'Transactions', link: '/transactions', icon: faReceipt, color: '#00BCD4' },
        { label: 'Calendar', link: '/calendar', icon: faCalendarDays, color: '#00796B' },
        { label: 'Settings', link: '#', icon: faGear, color: '#9E9E9E' },
      ],
    },
  ];

 
  constructor(
    private router: Router,
      private route: ActivatedRoute,
    private jobService: JobService ,private zone: NgZone,private loader:LoaderService
  ) {}
 
navigateWithSS(link: string): void {
  const ss_id = this.route.snapshot.queryParamMap.get('ss_id') || localStorage.getItem('ss_id');
  this.router.navigate([link], { queryParams: { ss_id } });
}
onItemClickAndNavigate(item: SidebarItem): void {
  const ss_id = this.route.snapshot.queryParamMap.get('ss_id') || localStorage.getItem('randomToken');
 
  // Save ss_id in localStorage just in case it's missing from query params
  if (ss_id) localStorage.setItem('ss_id', ss_id);
 
  this.sectionSelected.emit(item.label); // still emit section
  this.router.navigate([item.link], { queryParams: { ss_id } });
}
 
  ngOnInit(): void {
      console.log('SidenavComponent initialized');
 
  this.currentUrl = this.router.url;
 
  const roleID = JSON.parse(localStorage.getItem('userDetails') || '{}')?.roleID;
  if (roleID) {
    this.loader.show();
    this.jobService.getPagePermissionsByRole(roleID)
    .pipe(finalize(() => this.loader.hide()))
    .subscribe({
      next: (permissions: any[]) => {        
        this.buildSidebar(permissions);      },
      error: (err) => {
        this.loader.hide();
        console.error('Error fetching permissions:', err);
        setTimeout(() => {
              window.location.reload();
        }, 1000);
        this.logout()
        this.groupedSidebarItems = [];
 
      }
    });
  } else {
    console.warn('Role ID missing');
    this.groupedSidebarItems = [];
    this.logout()
    this.router.navigate(['/login']);
 
  }
}
 
 
 private buildSidebar(viewablePages: any[]): void {
  console.log('Pages from API:', viewablePages.map(p => p.pageUrl));
 
  this.groupedSidebarItems = this.allSidebarItems
    .map(group => {
      const filteredItems = group.items.filter(item => {
        const match = viewablePages.some(p => p.pageUrl.trim().toLowerCase() === item.link.trim().toLowerCase());
        if (!match) {
          console.warn(`No match for sidebar item: "${item.link}"`);
        }
        return match;
      });
 
      return { ...group, items: filteredItems };
    })
    .filter(group => group.items.length > 0);
 
  console.log('Final sidebar items:', this.groupedSidebarItems);
}
 
 
  dropItem(event: CdkDragDrop<SidebarItem[]>, group: SidebarGroup): void {
    moveItemInArray(group.items, event.previousIndex, event.currentIndex);
  }
 
  onItemClick(label: string): void {
    this.sectionSelected.emit(label);
  }
    logout(): void {
    // Clear session/local storage
    localStorage.removeItem('userDetails');
    localStorage.removeItem('ss_id');
    localStorage.removeItem('randomToken');
    // Redirect to login
    this.router.navigate(['/login']);
  }
} 