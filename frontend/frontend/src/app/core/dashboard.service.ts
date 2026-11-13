import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DashboardData } from '../shared/models/dashboard.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private http: HttpClient) {}

  getDashboard() {
    return this.http.get<DashboardData>(`${environment.apiUrl}/dashboard`);
  }
}