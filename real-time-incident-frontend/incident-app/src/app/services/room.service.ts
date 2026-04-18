import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Room, RoomDetail, RoomMember, ChatMessage, ActivityEntry } from '../models/models';

@Injectable({ providedIn: 'root' })
export class RoomService {
  private base = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getRooms(): Observable<Room[]> {
    return this.http.get<Room[]>(`${this.base}/rooms`);
  }

  getRoom(id: string): Observable<RoomDetail> {
    return this.http.get<RoomDetail>(`${this.base}/rooms/${id}`);
  }

  createRoom(title: string, description: string, severity: string): Observable<Room> {
    return this.http.post<Room>(`${this.base}/rooms`, { title, description, severity });
  }

  joinRoom(id: string): Observable<unknown> {
    return this.http.post(`${this.base}/rooms/${id}/join`, {});
  }

  updateStatus(id: string, status: string): Observable<unknown> {
    return this.http.put(`${this.base}/rooms/${id}/status`, { status });
  }

  getMembers(id: string): Observable<RoomMember[]> {
    return this.http.get<RoomMember[]>(`${this.base}/rooms/${id}/members`);
  }

  getMessages(id: string, page = 1, pageSize = 50): Observable<ChatMessage[]> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ChatMessage[]>(`${this.base}/rooms/${id}/messages`, { params });
  }

  getActivity(id: string, page = 1): Observable<ActivityEntry[]> {
    const params = new HttpParams().set('page', page);
    return this.http.get<ActivityEntry[]>(`${this.base}/rooms/${id}/activity`, { params });
  }
}
