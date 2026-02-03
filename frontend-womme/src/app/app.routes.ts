import { Routes,NavigationExtras } from '@angular/router';
import { ManHourLoginComponent } from './home/man-hour-login/man-hour-login';
// import { JobStartComponent }    from './job-work/job-start/job-start';
import {JobSyncComponent }  from './admin/job-sync/job-sync';
import { AdminDashboardComponent } from './admin/admin-dashboard/admin-dashboard';
import{ForgotPasswordComponent} from './home/forgot-password/forgot-password';
import { UnpostedJobTransaction } from './admin/unposted-job-transaction/unposted-job-transaction';
import { PostedJobTransaction } from './admin/posted-job-transaction/posted-job-transaction';
import { EmployeesComponent } from './admin/employees/employees';
import { MachineComponent } from './admin/machines/machines';
import{AssignJobComponent} from './admin/assign-jobs/assign-jobs'
import{OperationMastersComponent} from './admin/operation/operation';
import { RoleMasterComponent } from './admin/rolemaster/rolemaster';
import{ItemComponent} from './admin/item/item';
import{ReportsViewComponent} from './admin/report/reports-view';
import { JobDetailComponent } from './admin/job-details/job-details';
import{AuthGuard} from './services/auth.guard';
import { EditProfileComponent } from './admin/edit-profile/edit-profile';
import { CalendarComponent } from './admin/calendar/calendar';
import { QualityChecker } from './admin/qualitychecker/qualitychecker';
import { JobPoolDetails } from './admin/job-pool-details/job-pool-details';
 
import { JobSyncDetailComponent } from './admin/job-sync-details/job-sync-details';
import { JobPostedDetailComponent } from './admin/job-posted-details/job-posted-details';
import { JobListComponent } from './admin/job-list/job-list';
import { workcenter } from './admin/workcenter/workcenter';
import { MachineEmployeeComponent } from './admin/machine-employee/machine-employee';
import { LoaderComponent } from './home/loader/loader';    
import { DashboardOverviewComponent } from './admin/dashboard_admin/dashboard_admin';
import { ProductionQcDashboardComponent } from './admin/dashboard_emp/dashboard_emp';
import { QcDashboardComponent } from './admin/dashboard_qc/dashboard_qc';
import { Issuetransaction } from './admin/issuetransaction/issuetransaction';
import { VerifyTransaction } from './admin/verify-transaction/verify-transaction';
import { DashboardVerify } from './admin/dashboard-verify/dashboard-verify';
import { JobReportComponent } from './admin/job-report/job-report';
import { NotificationComponent } from './admin/notifications/notifications';
 
export const routes: Routes = [
 
  { path: '', component: ManHourLoginComponent },  
  { path: 'dashboard', component: AdminDashboardComponent, canActivate: [AuthGuard] },
  { path: 'notifications', component: NotificationComponent, canActivate: [AuthGuard] },
  { path: 'forgot-password', component: ForgotPasswordComponent, canActivate: [AuthGuard] },
  { path: 'jobs', component: JobSyncComponent, canActivate: [AuthGuard] },
  { path: 'issuejobtransaction', component: Issuetransaction, canActivate: [AuthGuard] },
  { path: 'verify-transaction', component: VerifyTransaction, canActivate: [AuthGuard] },
  { path: 'unpostedjobtransaction', component: UnpostedJobTransaction, canActivate: [AuthGuard] },
  { path: 'postedjobtransaction', component: PostedJobTransaction, canActivate: [AuthGuard] },
  { path: 'reports', component: ReportsViewComponent, canActivate: [AuthGuard] },
  { path: 'job-details', component: JobDetailComponent, canActivate: [AuthGuard] },
  { path: 'users', component: EmployeesComponent, canActivate: [AuthGuard] },
  { path: 'machines', component: MachineComponent, canActivate: [AuthGuard] },
  { path: 'operations', component: OperationMastersComponent, canActivate: [AuthGuard] },
  { path: 'items', component: ItemComponent, canActivate: [AuthGuard] },
  { path: 'rolemaster', component: RoleMasterComponent, canActivate: [AuthGuard] },
  { path: 'assignjob', component: AssignJobComponent, canActivate: [AuthGuard] },
  { path: 'admin/edit-profile', component: EditProfileComponent , canActivate: [AuthGuard]},
  { path:'calendar',component:CalendarComponent , canActivate: [AuthGuard]},
  { path:'qualitychecker',component:QualityChecker, canActivate: [AuthGuard]},
  { path:'job-pool-details',component:JobPoolDetails, canActivate: [AuthGuard]},
  { path:'job-report',component:JobReportComponent, canActivate: [AuthGuard]},
 
  {path:'JobListComponent',component:JobListComponent,canActivate: [AuthGuard]},
  {path:'job-sync-details',component:JobSyncDetailComponent , canActivate: [AuthGuard]},
  {path:'job-posted-details',component:JobPostedDetailComponent , canActivate: [AuthGuard]},
  {path:'workcenter',component:workcenter , canActivate: [AuthGuard]},
  {path:'machine-employee',component:MachineEmployeeComponent , canActivate: [AuthGuard]},
  {path:'loader',component:LoaderComponent , canActivate: [AuthGuard]},
 
  {path:'dashboard_admin',component:DashboardOverviewComponent , canActivate: [AuthGuard]},
  {path:'dashboard_emp',component:ProductionQcDashboardComponent , canActivate: [AuthGuard]},
  {path:'dashboard_qc',component:QcDashboardComponent , canActivate: [AuthGuard]},
  {path:'dashboard_verify',component:DashboardVerify , canActivate: [AuthGuard]},
 
  { path: '**', redirectTo: '' }
 
];
 