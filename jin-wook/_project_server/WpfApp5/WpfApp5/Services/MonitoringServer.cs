using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WpfApp5.Models;

namespace WpfApp5.Services
{
    /// <summary>
    /// TCP 서버를 관리하는 클래스입니다.
    /// 클라이언트의 연결을 받고, 메시지를 수신하며, 보안 검사를 수행합니다.
    /// </summary>
    public class MonitoringServer
    {
        /// <summary>
        /// 동시에 접속할 수 있는 최대 클라이언트 수
        /// 10명을 초과하면 새로운 연결을 거부합니다.
        /// </summary>
        public const int MAX_CLIENTS = 10;

        /// <summary>
        /// 서버가 사용할 포트 번호
        /// 클라이언트는 이 포트로 연결해야 합니다.
        /// </summary>
        private const int SERVER_PORT = 5000;

        /// <summary>
        /// TCP 리스너 객체
        /// 클라이언트의 연결 요청을 받기 위해 사용됩니다.
        /// </summary>
        private TcpListener _listener;

        /// <summary>
        /// 현재 연결된 클라이언트들을 저장하는 딕셔너리
        /// 키: IP 주소 (문자열), 값: ClientInfo 객체
        /// ConcurrentDictionary를 사용하여 여러 스레드에서 안전하게 접근할 수 있습니다.
        /// </summary>
        private readonly ConcurrentDictionary<string, ClientInfo> _clients;

        /// <summary>
        /// 동시 접속자 수를 제한하기 위한 세마포어
        /// 최대 10명까지만 접속을 허용하고, 초과 시 연결을 거부합니다.
        /// </summary>
        private readonly SemaphoreSlim _connectionSemaphore;

        /// <summary>
        /// 서버 작업을 취소하기 위한 토큰 소스
        /// 서버를 중지할 때 모든 비동기 작업을 안전하게 중단합니다.
        /// </summary>
        private CancellationTokenSource _serverCancellationToken;

        /// <summary>
        /// 서버가 현재 실행 중인지 여부
        /// </summary>
        private bool _isRunning = false;

        // ========== 이벤트 정의 ==========
        // 이벤트는 특정 상황이 발생했을 때 다른 클래스에 알려주는 메커니즘입니다.
        // 예: 클라이언트가 연결되면 ClientConnected 이벤트가 발생합니다.

        /// <summary>
        /// 클라이언트가 연결되었을 때 발생하는 이벤트
        /// 매개변수: 연결된 클라이언트의 IP 주소
        /// </summary>
        public event Action<string> ClientConnected;

        /// <summary>
        /// 클라이언트 연결이 끊어졌을 때 발생하는 이벤트
        /// 매개변수: 연결이 끊어진 클라이언트의 IP 주소
        /// </summary>
        public event Action<string> ClientDisconnected;

        /// <summary>
        /// 클라이언트로부터 URL을 받았을 때 발생하는 이벤트
        /// 매개변수1: 클라이언트의 IP 주소
        /// 매개변수2: 받은 URL
        /// </summary>
        public event Action<string, string> UrlReceived;

        /// <summary>
        /// 차단된 사이트에 접속이 감지되었을 때 발생하는 이벤트
        /// 매개변수1: 클라이언트의 IP 주소
        /// 매개변수2: 차단된 URL
        /// </summary>
        public event Action<string, string> SecurityAlert;

        /// <summary>
        /// 생성자: MonitoringServer 객체를 초기화합니다.
        /// </summary>
        public MonitoringServer()
        {
            // 클라이언트 정보를 저장할 딕셔너리 초기화
            _clients = new ConcurrentDictionary<string, ClientInfo>();

            // 최대 10명까지 접속을 허용하는 세마포어 초기화
            // 첫 번째 매개변수: 초기 허용 수, 두 번째 매개변수: 최대 허용 수
            _connectionSemaphore = new SemaphoreSlim(MAX_CLIENTS, MAX_CLIENTS);
        }

        /// <summary>
        /// 서버가 현재 실행 중인지 확인하는 속성
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 서버를 시작하는 메서드입니다.
        /// 포트 5000에서 클라이언트의 연결을 받기 시작합니다.
        /// </summary>
        public async Task StartAsync()
        {
            // 이미 실행 중이면 중복 실행을 방지
            if (_isRunning) return;

            // 서버 작업 취소 토큰 초기화
            _serverCancellationToken = new CancellationTokenSource();

            // TCP 리스너 생성 및 시작
            // IPAddress.Any: 모든 네트워크 인터페이스에서 연결을 받음
            _listener = new TcpListener(IPAddress.Any, SERVER_PORT);
            _listener.Start();
            _isRunning = true;

            // 클라이언트 연결을 받는 비동기 작업 시작
            // _ = 는 반환값을 무시한다는 의미입니다 (fire-and-forget 패턴)
            _ = AcceptClientsAsync(_serverCancellationToken.Token);
        }

        /// <summary>
        /// 클라이언트의 연결 요청을 계속 받는 메서드입니다.
        /// 새로운 클라이언트가 연결되면 HandleClientAsync를 호출하여 처리합니다.
        /// </summary>
        /// <param name="cancellationToken">작업 취소 토큰</param>
        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            // 서버가 중지될 때까지 무한 반복
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    // 클라이언트의 연결 요청을 기다립니다 (비동기)
                    // 클라이언트가 연결하면 TcpClient 객체가 반환됩니다
                    var client = await _listener.AcceptTcpClientAsync();

                    // 연결된 클라이언트를 처리하는 작업을 시작합니다
                    // 각 클라이언트는 별도의 작업(Task)으로 처리되므로 동시에 여러 클라이언트를 처리할 수 있습니다
                    _ = HandleClientAsync(client, cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    // 리스너가 종료되었을 때 (정상적인 종료)
                    break;
                }
                catch (Exception)
                {
                    // 기타 예외 발생 시 로깅 가능
                    break;
                }
            }
        }

        /// <summary>
        /// 개별 클라이언트의 연결을 처리하는 메서드입니다.
        /// 클라이언트로부터 메시지를 받고, 연결이 끊어질 때까지 대기합니다.
        /// </summary>
        /// <param name="client">연결된 TCP 클라이언트 객체</param>
        /// <param name="cancellationToken">작업 취소 토큰</param>
        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            // ========== 최대 접속자 수 체크 ==========
            // SemaphoreSlim.WaitAsync(0)는 즉시 사용 가능한 슬롯이 있는지 확인합니다
            // 0을 전달하면 대기하지 않고 즉시 반환합니다
            // false를 반환하면 최대 접속자 수를 초과했다는 의미입니다
            if (!await _connectionSemaphore.WaitAsync(0, cancellationToken))
            {
                // 최대 접속자 수를 초과했으므로 연결을 거부합니다
                try
                {
                    client.Close();
                }
                catch { }
                return;
            }

            // 클라이언트 정보를 저장할 변수들
            string ip = null;
            ClientInfo clientInfo = null;

            try
            {
                // ========== 클라이언트 정보 추출 ==========
                // RemoteEndPoint에서 클라이언트의 IP 주소를 가져옵니다
                ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                // 클라이언트 정보 객체 생성
                clientInfo = new ClientInfo
                {
                    IpAddress = ip,                                    // IP 주소 저장
                    Client = client,                                  // TCP 클라이언트 객체 저장
                    Stream = client.GetStream(),                       // 네트워크 스트림 가져오기
                    ConnectedAt = DateTime.Now,                       // 연결 시간 기록
                    CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)  // 취소 토큰 생성
                };

                // ========== 클라이언트 등록 ==========
                // 딕셔너리에 클라이언트 정보를 추가합니다
                // TryAdd는 성공하면 true, 실패하면 false를 반환합니다
                // 실패하는 경우: 같은 IP가 이미 등록되어 있을 때
                if (!_clients.TryAdd(ip, clientInfo))
                {
                    // 이미 등록된 IP이므로 연결을 종료합니다
                    client.Close();
                    return;
                }

                // 클라이언트 연결 이벤트 발생 (UI에 알림)
                ClientConnected?.Invoke(ip);

                // 클라이언트로부터 메시지를 받는 작업 시작
                await ProcessClientMessagesAsync(clientInfo);
            }
            catch (Exception)
            {
                // 예외 발생 시 로깅 가능
            }
            finally
            {
                // ========== 리소스 정리 ==========
                // finally 블록은 예외가 발생하더라도 반드시 실행됩니다
                // 연결이 정상적으로 종료되거나 예외가 발생했을 때 리소스를 정리합니다
                if (clientInfo != null)
                {
                    // 딕셔너리에서 클라이언트 제거
                    _clients.TryRemove(ip, out _);

                    // 취소 토큰으로 작업 중단
                    clientInfo.CancellationTokenSource?.Cancel();
                    clientInfo.CancellationTokenSource?.Dispose();

                    // 네트워크 리소스 정리
                    try
                    {
                        clientInfo.Stream?.Dispose();  // 스트림 닫기
                        clientInfo.Client?.Close();     // 클라이언트 연결 닫기
                    }
                    catch { }

                    // 세마포어 해제 (다른 클라이언트가 접속할 수 있도록)
                    _connectionSemaphore.Release();

                    // 클라이언트 연결 해제 이벤트 발생 (UI에 알림)
                    ClientDisconnected?.Invoke(ip);
                }
            }
        }

        /// <summary>
        /// 클라이언트로부터 메시지를 받는 메서드입니다.
        /// 클라이언트가 보내는 URL을 계속 받아서 처리합니다.
        /// </summary>
        /// <param name="clientInfo">클라이언트 정보 객체</param>
        private async Task ProcessClientMessagesAsync(ClientInfo clientInfo)
        {
            // 데이터를 받을 버퍼 (최대 1024바이트)
            byte[] buffer = new byte[1024];

            // 클라이언트 연결이 유지되는 동안 계속 반복
            while (!clientInfo.CancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // ========== 메시지 수신 ==========
                    // 네트워크 스트림에서 데이터를 읽어옵니다 (비동기)
                    // bytesRead: 실제로 읽은 바이트 수
                    int bytesRead = await clientInfo.Stream.ReadAsync(
                        buffer, 0, buffer.Length,
                        clientInfo.CancellationTokenSource.Token);

                    // bytesRead가 0이면 클라이언트가 연결을 끊었다는 의미입니다
                    if (bytesRead == 0) break;

                    // ========== 데이터 처리 ==========
                    // 받은 바이트 데이터를 UTF-8 문자열로 변환합니다
                    // ToLower(): 대소문자 구분 없이 비교하기 위해 소문자로 변환
                    // Trim(): 앞뒤 공백 제거
                    string url = Encoding.UTF8.GetString(buffer, 0, bytesRead)
                        .ToLower().Trim();

                    // 클라이언트 정보에 현재 URL 저장
                    clientInfo.CurrentUrl = url;

                    // URL 수신 이벤트 발생 (UI에 알림)
                    UrlReceived?.Invoke(clientInfo.IpAddress, url);
                }
                catch (OperationCanceledException)
                {
                    // 작업이 취소되었을 때 (정상적인 종료)
                    break;
                }
                catch (Exception)
                {
                    // 기타 예외 발생 시 연결 종료
                    break;
                }
            }
        }

        /// <summary>
        /// 보안 검사를 수행하는 메서드입니다.
        /// URL에 차단 키워드가 포함되어 있는지 확인하고, 발견되면 클라이언트에 경고를 보냅니다.
        /// </summary>
        /// <param name="ip">클라이언트의 IP 주소</param>
        /// <param name="url">검사할 URL</param>
        /// <param name="forbiddenKeywords">차단할 키워드 목록</param>
        public void CheckSecurityAlert(string ip, string url, List<string> forbiddenKeywords)
        {
            // 유효성 검사: URL이나 키워드 목록이 비어있으면 검사를 수행하지 않습니다
            if (string.IsNullOrEmpty(url) || forbiddenKeywords == null || forbiddenKeywords.Count == 0)
                return;

            // 각 차단 키워드를 확인합니다
            foreach (var keyword in forbiddenKeywords)
            {
                // 키워드 앞뒤 공백 제거 및 소문자 변환
                string trimmedKeyword = keyword.Trim().ToLower();
                if (string.IsNullOrEmpty(trimmedKeyword)) continue;

                // ========== 차단 키워드 검사 ==========
                // URL에 차단 키워드가 포함되어 있는지 확인합니다
                if (url.Contains(trimmedKeyword))
                {
                    // 클라이언트 정보를 가져옵니다
                    if (_clients.TryGetValue(ip, out var clientInfo))
                    {
                        try
                        {
                            // ========== 경고 메시지 전송 ==========
                            // 클라이언트에 ALERT 메시지를 보냅니다
                            string alertMessage = $"ALERT: [{trimmedKeyword}] 접속이 감지되었습니다. 즉시 종료하세요!";
                            byte[] alertData = Encoding.UTF8.GetBytes(alertMessage);
                            clientInfo.Stream.Write(alertData, 0, alertData.Length);

                            // 보안 경고 이벤트 발생 (UI에 알림)
                            SecurityAlert?.Invoke(ip, url);
                        }
                        catch { }
                    }
                    // 하나의 키워드라도 발견되면 더 이상 검사하지 않습니다
                    break;
                }
            }
        }

        /// <summary>
        /// 서버를 중지하는 메서드입니다.
        /// 모든 클라이언트 연결을 종료하고 리소스를 정리합니다.
        /// </summary>
        public void Stop()
        {
            // 이미 중지된 상태면 아무것도 하지 않습니다
            if (!_isRunning) return;

            // 서버 실행 상태를 false로 변경
            _isRunning = false;

            // 모든 비동기 작업을 취소합니다
            _serverCancellationToken?.Cancel();

            // TCP 리스너 중지
            try
            {
                _listener?.Stop();
            }
            catch { }

            // ========== 모든 클라이언트 연결 종료 ==========
            // 현재 연결된 모든 클라이언트의 연결을 종료합니다
            foreach (var client in _clients.Values)
            {
                try
                {
                    client.CancellationTokenSource?.Cancel();  // 작업 취소
                    client.Stream?.Dispose();                   // 스트림 닫기
                    client.Client?.Close();                     // 연결 닫기
                }
                catch { }
            }

            // 클라이언트 딕셔너리 비우기
            _clients.Clear();

            // 취소 토큰 소스 정리
            _serverCancellationToken?.Dispose();
        }

        /// <summary>
        /// 현재 연결된 클라이언트 수를 반환합니다.
        /// </summary>
        /// <returns>연결된 클라이언트 수</returns>
        public int GetConnectedClientCount() => _clients.Count;

        /// <summary>
        /// 현재 연결된 모든 클라이언트의 IP 주소 목록을 반환합니다.
        /// </summary>
        /// <returns>IP 주소 목록</returns>
        public IEnumerable<string> GetClientIps() => _clients.Keys;

        /// <summary>
        /// 특정 클라이언트의 현재 URL을 가져옵니다.
        /// </summary>
        /// <param name="ip">클라이언트의 IP 주소</param>
        /// <returns>현재 URL (없으면 null)</returns>
        public string GetClientUrl(string ip)
        {
            // 딕셔너리에서 클라이언트 정보를 찾아서 URL을 반환합니다
            return _clients.TryGetValue(ip, out var info) ? info.CurrentUrl : null;
        }
    }
}
