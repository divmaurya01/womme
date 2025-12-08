import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Workcenter } from './workcenter';

describe('Workcenter', () => {
  let component: Workcenter;
  let fixture: ComponentFixture<Workcenter>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Workcenter]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Workcenter);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
