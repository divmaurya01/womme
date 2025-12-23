import { CommonModule } from '@angular/common';
import { Component, Input, Output, EventEmitter, ViewEncapsulation, OnInit} from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faTachometerAlt, faRepeat, faSyncAlt, faUsers, faBook, faCog, faProjectDiagram, faUpload, faSitemap, faUserShield,faCalendar, faIndustry, faBox, faTicket, faTruckLoading} from '@fortawesome/free-solid-svg-icons';
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
        { label: 'Dashboard', link:'/dashboard_admin', icon: faTachometerAlt, color: '#3E7CB1' },
        { label: 'Dashboard', link:'/dashboard_emp', icon: faTachometerAlt, color: '#3E7CB1' },
        { label: 'Dashboard', link:'/dashboard_qc', icon: faTachometerAlt, color: '#3E7CB1' },
        { label: 'Dashboard', link:'/dashboard_verify', icon: faTachometerAlt, color: '#3E7CB1' },
       
        //{ label: 'Dashboard', link:'/dashboard', icon: faTachometerAlt, color: '#3E7CB1' },        
        { label: 'Job Sync', link: '/jobs', icon: faSyncAlt, color: '#00A6A6' },
        { label: 'Issue Transaction', link: '/issuejobtransaction', icon: faRepeat, color: '#d015e9ff' },
        { label: 'Verify Transaction', link: '/verify-transaction', icon: faTruckLoading, color: '#01071dff' },
        { label: 'Unposted Transaction', link: '/unpostedjobtransaction', icon: faRepeat, color: '#E9A115' },
        { label: 'Posted Transaction', link: '/postedjobtransaction', icon: faUpload, color: '#4AA96C' },
        { label: 'Quality check', link: '/qualitychecker', icon: faTicket, color: '#70eaadff' },
        { label: 'Main Reports', link: '/JobListComponent', icon: faBook, color: '#70eae6ff' },
        { label: 'Job Report', link: '/job-report', icon: faBook, color: '#70eae6ff' },       
      ],
    },
    {
      title: 'Manage',
      items: [
        { label: 'WC/M Mapping', link: '/machine-employee', icon: faCog, color: '#ffc107' },
        { label: 'WC/E Mapping', link: '/workcenter', icon: faSitemap, color: '#816CDA' },
        { label: 'Machines', link: '/machines', icon: faIndustry, color: '#8C52FF' },
        { label: 'Employee', link: '/users', icon: faUsers, color: '#279EFF' },
        // { label: 'Assign Job', link: '/assignjob', icon: faIndustry, color: '#816CDA' },
        { label: 'Operations', link: '/operations', icon: faProjectDiagram, color: '#E85A70' },        
        { label: 'Item', link: '/items', icon: faBox, color: '#5F8D4E' },        
        { label: 'Role Master', link: '/rolemaster', icon: faUserShield, color: '#F4A261' },
        { label: 'TransactionComponent', link: '/transactions', icon: faBook, color: '#17a2b8' },
        { label: 'Settings', link: '#', icon: faCog, color: '#ffc107' },
        { label: 'Calendar', link: '/calendar', icon: faCalendar, color: '#06675aff' },
 
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