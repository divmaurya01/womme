import { ComponentFixture, TestBed } from '@angular/core/testing';
import { JobSyncDetailComponent } from './job-sync-details';

describe('JobSyncDetailComponent', () => {
  let component: JobSyncDetailComponent;
  let fixture: ComponentFixture<JobSyncDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JobSyncDetailComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(JobSyncDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
