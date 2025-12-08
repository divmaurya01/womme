import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AssignJobs } from './assign-jobs';

describe('AssignJobs', () => {
  let component: AssignJobs;
  let fixture: ComponentFixture<AssignJobs>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssignJobs]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AssignJobs);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
