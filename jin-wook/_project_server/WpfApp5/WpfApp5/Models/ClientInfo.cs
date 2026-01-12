using System;
using System.Net.Sockets;

namespace WpfApp5.Models
{
    /// <summary>
    /// 클라이언트 정보를 저장하는 데이터 클래스입니다.
    /// 각 클라이언트의 연결 정보와 현재 상태를 관리합니다.
    /// </summary>
    public class ClientInfo
    {
        /// <summary>
        /// 클라이언트의 IP 주소 (예: "192.168.1.100")
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// 클라이언트가 현재 접속 중인 웹사이트 URL
        /// </summary>
        public string CurrentUrl { get; set; }

        /// <summary>
        /// TCP 클라이언트 연결 객체
        /// 서버와 클라이언트 간의 네트워크 연결을 나타냅니다.
        /// </summary>
        public TcpClient Client { get; set; }

        /// <summary>
        /// 네트워크 스트림 객체
        /// 클라이언트와 데이터를 주고받기 위한 통로입니다.
        /// </summary>
        public NetworkStream Stream { get; set; }

        /// <summary>
        /// 클라이언트가 서버에 연결된 시간
        /// </summary>
        public DateTime ConnectedAt { get; set; }

        /// <summary>
        /// 작업 취소 토큰 소스
        /// 비동기 작업을 안전하게 중단하기 위해 사용됩니다.
        /// 예: 클라이언트 연결이 끊어졌을 때 메시지 수신 작업을 중단
        /// </summary>
        public System.Threading.CancellationTokenSource CancellationTokenSource { get; set; }
    }
}
