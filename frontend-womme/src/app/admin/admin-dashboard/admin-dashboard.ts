import { Component, AfterViewInit, OnInit, OnDestroy, ViewChildren, QueryList } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NgChartsModule, BaseChartDirective } from 'ng2-charts';
import { ChartData } from 'chart.js';
import { HeaderComponent } from '../header/header';
import { SidenavComponent } from '../sidenav/sidenav';
import { JobService } from '../../services/job.service';
import { AuthServices } from '../../services/auth.service';
import { LoaderService } from '../../services/loader.service';
import { finalize } from 'rxjs/operators';


@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  templateUrl: './admin-dashboard.html',
  styleUrls: ['./admin-dashboard.scss'],
  imports: [CommonModule, RouterModule, NgChartsModule, HeaderComponent, SidenavComponent]
})


export class AdminDashboardComponent implements OnInit, AfterViewInit, OnDestroy {
  isSidebarHidden = false;
  constructor(private jobService: JobService, private loader: LoaderService) { }

  @ViewChildren(BaseChartDirective) charts!: QueryList<BaseChartDirective>;
  private resizeListener!: () => void;


  ngOnInit(): void {
    this.resizeListener = () => {
      this.charts.forEach(chart => chart.chart?.resize());
    };
    window.addEventListener('resize', this.resizeListener);

    this.loadDashboardStats();
    this.loadUserChartData();
    this.testTransactionOverview();
    this.testTransactionData();
  }

  activityCounts = {
    completedJobs: 0,
    activeJobs: 0,
    holdJobs: 0,
    overtimeJobs: 0
  };
  testTransactionOverview() {
    const payload = {
      todayOnly: 0,
      includeTransaction: 1,
      includeQC: 1,
      includeVerify: 1  
    };

    this.jobService.GetTransactionOverview(payload).subscribe({
      next: (res) => {
        console.log('Transaction Overview API Response:', res);
      },
      error: (err) => {
        console.error('Error calling GetTransactionOverview:', err);
      }
    });
  }

  testTransactionData() {
  const payload = {
    TodayOnly: 0,
    IncludeTransaction: 1,
    IncludeQC: 1,
    IncludeVerify: 1,  
    PageNumber: 1,  
    PageSize: 50
  };


  this.jobService.GetTransactionData(payload).subscribe({
    next: (res) => {
      console.log('Transaction Data API Response:', res);
    },
    error: (err) => {
      console.error('Error calling GetTransactionData:', err);
    }
  });
}

  loadDashboardStats() {
    this.jobService.getTotalUsers().subscribe(res => {
      this.updateStat('users', this.extractNumber(res, 'totalUsers'));
    });

    this.jobService.getunpostedJobs().subscribe(res => {
      this.updateStat('jobs', this.extractNumber(res, 'totalUnpostedJobs'));
    });

    this.jobService.PostedJobsCount().subscribe(res => {
      const val = this.extractNumber(res, 'totalPostedJobs');
      this.updateStat('Postedjobs', val);
      this.activityCounts.completedJobs = val;
      this.updateActivityChartData();
    });

    this.jobService.ActiveJobsCount().subscribe(res => {
      const val = this.extractNumber(res, 'active');
      this.updateStat('active', val);
      this.activityCounts.activeJobs = val;
      this.updateActivityChartData();
    });

    this.jobService.PausedJobsCount().subscribe(res => {
      const val = this.extractNumber(res, 'paused');
      this.updateStat('hold', val);
      this.activityCounts.holdJobs = val;
      this.updateActivityChartData();
    });

    this.jobService.excdJobsCount().subscribe(res => {
      const val = this.extractNumber(res, 'excdJobs');
      this.updateStat('excdJobs', val);
      this.activityCounts.overtimeJobs = val;
      this.updateActivityChartData();
    });
  }


  extractNumber(res: any, key: string): number {
    return typeof res === 'object' && res !== null && key in res ? res[key] : Number(res);
  }


  updateStat(key: string, value: number) {
    const stat = this.systemStats.find(s => s.key === key);
    if (stat) {
      stat.count = value;
    }
  }

  systemStats = [
    { key: 'users', title: 'Users', count: 0, color: 'soft-blue' },
    { key: 'jobs', title: 'UnPosted Jobs', count: 0, color: 'soft-red' },
    { key: 'Postedjobs', title: 'Posted Jobs', count: 0, color: 'soft-cyan' },
    { key: 'active', title: 'Active Jobs', count: 0, color: 'soft-yellow' },
    { key: 'hold', title: 'Paused Jobs', count: 0, color: 'soft-purple' },
    { key: 'excdJobs', title: 'Excited Jobs', count: 0, color: 'soft-green' },
  ];




  loadUserChartData() {
    this.jobService.UserMaster().subscribe(users => {
      const roleCounts = {
        Admins: 0,
        'Factory Managers': 0,
        Suppliers: 0,
        Workers: 0
      };

      users.forEach((user: any) => {
        switch (user.roleName) {
          case 'Admin':
            roleCounts.Admins++;
            break;
          case 'Factory Manager':
            roleCounts['Factory Managers']++;
            break;
          case 'Supervisor':
            roleCounts.Suppliers++; // Assuming "Supervisor" maps to "Supplier"
            break;
          case 'Worker':
            roleCounts.Workers++;
            break;
        }
      });

      this.userChartData = {
        labels: ['Admins', 'Factory Managers', 'Suppliers', 'Workers'],
        datasets: [
          {
            data: [
              roleCounts.Admins,
              roleCounts['Factory Managers'],
              roleCounts.Suppliers,
              roleCounts.Workers
            ],
            backgroundColor: ['#C8BAB2', '#e1dc6cff', '#7eb39bff', '#c2a286ff']
          }
        ]
      };
    });
  }


  updateActivityChartData() {
    this.activityChartData = {
      labels: ['Completed Jobs', 'Active Jobs', 'Hold Jobs', 'Active Over-Time Jobs'],
      datasets: [
        {
          label: 'Activity Count',
          data: [
            this.activityCounts.completedJobs,
            this.activityCounts.activeJobs,
            this.activityCounts.holdJobs,
            this.activityCounts.overtimeJobs
          ],
          backgroundColor: ['#C4832D', '#428588', '#724A59', '#AFA188']
        }
      ]
    };

    this.charts?.forEach(chart => chart.update()); // Ensure the chart refreshes
  }



  userChartData: ChartData<'pie'> = {
    labels: [],
    datasets: []
  };





  activityChartData: ChartData<'bar'> = {
    labels: [],
    datasets: []
  };




  ngAfterViewInit(): void {
    this.resizeListener = () => {
      this.charts.forEach(chart => chart.chart?.resize());
    };
    window.addEventListener('resize', this.resizeListener);
  }

  ngOnDestroy(): void {
    window.removeEventListener('resize', this.resizeListener);
  }

  jobs = [
    { id: 1, title: 'Job A', status: 'Open', hours: 12, assignee: 'John' },
    { id: 2, title: 'Job B', status: 'In Progress', hours: 18, assignee: 'Mary' },
    { id: 3, title: 'Job C', status: 'Review', hours: 7, assignee: 'Anna' },
    { id: 4, title: 'Job D', status: 'Closed', hours: 14, assignee: 'Paul' }
  ];


  activeWorkers = [
    { code: 'W001', name: 'John Doe', dept: 'Assembly' },
    { code: 'W002', name: 'Jane Smith', dept: 'Welding' },
    // ...from your API
  ];

  activeMachines = [
    { name: 'Press Machine', code: 'M001', workcenter: 'WC-A' },
    { name: 'Lathe Machine', code: 'M002', workcenter: 'WC-B' },
    // ...from your API
  ];


  toggleSidebar() {
    console.log('Toggle clicked'); // should print
    this.isSidebarHidden = !this.isSidebarHidden;
    console.log('Sidebar hidden:', this.isSidebarHidden); // Add this
  }
}
