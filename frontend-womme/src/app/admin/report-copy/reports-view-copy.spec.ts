import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReportsViewComponentCopy } from './reports-view-copy';

describe('ReportsViewComponentCopy', () => {
  let component: ReportsViewComponentCopy;
  let fixture: ComponentFixture<ReportsViewComponentCopy>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReportsViewComponentCopy]
    }).compileComponents();

    fixture = TestBed.createComponent(ReportsViewComponentCopy);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
