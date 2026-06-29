Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.Collections.Generic

Public Class Form1
    Inherits Form

    Private Const DEFAULT_PORT As Integer = 9988
    Private Const CELL_SIZE As Integer = 48
    Private Const TICK_MS As Integer = 500

    Private game As BombGame
    Private peer As NetworkPeer
    Private isHost As Boolean
    Private localPlayer As Integer = -1

    Private BoardW As Integer = BombGame.COLS * CELL_SIZE
    Private BoardH As Integer = BombGame.ROWS * CELL_SIZE

    ' === UI connect ===
    Private pnlConnect As Panel
    Private txtPort As TextBox
    Private txtIP As TextBox
    Private btnHost As Button
    Private btnJoin As Button
    Private lblStatus As Label

    ' === UI game ===
    Private pnlGame As Panel
    Private boardPanel As Panel
    Private lblInfo As Label
    Private lblYouAre As Label
    Private btnRestart As Button
    Private lstLog As ListBox

    ' === Timer (chi host chay) ===
    Private tickTimer As System.Windows.Forms.Timer

    ' FIX: flag tranh gui STATE lien tuc trong cung 1 tick
    Private statePending As Boolean = False

    Public Sub New()
        InitUI()
    End Sub

    Private Sub InitUI()
        Me.Text = "Dat Bom Online - 2CongLC"
        Me.ClientSize = New Size(BoardW + 20, BoardH + 220)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(30, 30, 30)
        Me.KeyPreview = True
        AddHandler Me.KeyDown, AddressOf Form1_KeyDown

        tickTimer = New System.Windows.Forms.Timer()
        tickTimer.Interval = TICK_MS
        AddHandler tickTimer.Tick, AddressOf TickTimer_Tick

        BuildConnectPanel()
        BuildGamePanel()
        pnlGame.Visible = False
    End Sub

    ' ============================================================
    '  CONNECT PANEL
    ' ============================================================
    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Dock = DockStyle.Fill
        pnlConnect.BackColor = Color.FromArgb(30, 30, 30)

        Dim lbl As New Label()
        lbl.Text = "DAT BOM ONLINE" : lbl.Font = New Font("Segoe UI", 22.0!, FontStyle.Bold)
        lbl.ForeColor = Color.OrangeRed
        lbl.Location = New Point(230, 70) : lbl.AutoSize = True
        pnlConnect.Controls.Add(lbl)

        Dim lbl2 As New Label()
        lbl2.Text = "Bomberman 2 Nguoi - PvP LAN/Online"
        lbl2.Font = New Font("Segoe UI", 10.0!)
        lbl2.ForeColor = Color.LightGray
        lbl2.Location = New Point(255, 115) : lbl2.AutoSize = True
        pnlConnect.Controls.Add(lbl2)

        Dim lPort As New Label() : lPort.Text = "Port:" : lPort.ForeColor = Color.White
        lPort.Location = New Point(300, 180) : lPort.AutoSize = True
        pnlConnect.Controls.Add(lPort)
        txtPort = New TextBox() : txtPort.Text = DEFAULT_PORT.ToString()
        txtPort.Location = New Point(355, 177) : txtPort.Width = 80
        pnlConnect.Controls.Add(txtPort)

        btnHost = New Button() : btnHost.Text = "Tao phong (Host)"
        btnHost.Location = New Point(300, 215) : btnHost.Size = New Size(200, 38)
        btnHost.BackColor = Color.OrangeRed : btnHost.ForeColor = Color.White
        btnHost.FlatStyle = FlatStyle.Flat
        AddHandler btnHost.Click, AddressOf BtnHost_Click
        pnlConnect.Controls.Add(btnHost)

        Dim lIP As New Label() : lIP.Text = "IP Host:" : lIP.ForeColor = Color.White
        lIP.Location = New Point(300, 275) : lIP.AutoSize = True
        pnlConnect.Controls.Add(lIP)
        txtIP = New TextBox() : txtIP.Text = "127.0.0.1"
        txtIP.Location = New Point(370, 272) : txtIP.Width = 140
        pnlConnect.Controls.Add(txtIP)

        btnJoin = New Button() : btnJoin.Text = "Vao phong (Join)"
        btnJoin.Location = New Point(300, 307) : btnJoin.Size = New Size(200, 38)
        btnJoin.BackColor = Color.SteelBlue : btnJoin.ForeColor = Color.White
        btnJoin.FlatStyle = FlatStyle.Flat
        AddHandler btnJoin.Click, AddressOf BtnJoin_Click
        pnlConnect.Controls.Add(btnJoin)

        lblStatus = New Label() : lblStatus.Location = New Point(255, 375) : lblStatus.AutoSize = True
        lblStatus.ForeColor = Color.LightGray
        lblStatus.Text = "Host: bam 'Tao phong'." & Environment.NewLine & "Khach: nhap IP roi bam 'Vao phong'."
        pnlConnect.Controls.Add(lblStatus)

        Dim lHelp As New Label()
        lHelp.Text = "Dieu khien (ca 2 may):" & Environment.NewLine &
                     "  WASD hoac Mui ten: di chuyen" & Environment.NewLine &
                     "  Space: dat bom"
        lHelp.ForeColor = Color.Yellow
        lHelp.Font = New Font("Segoe UI", 9.0!)
        lHelp.Location = New Point(255, 430) : lHelp.AutoSize = True
        pnlConnect.Controls.Add(lHelp)

        Me.Controls.Add(pnlConnect)
    End Sub

    ' ============================================================
    '  GAME PANEL
    ' ============================================================
    Private Sub BuildGamePanel()
        pnlGame = New Panel()
        pnlGame.Location = New Point(0, 0)
        pnlGame.Size = New Size(BoardW + 20, BoardH + 220)
        pnlGame.BackColor = Color.FromArgb(30, 30, 30)

        lblYouAre = New Label()
        lblYouAre.Location = New Point(10, 8) : lblYouAre.AutoSize = True
        lblYouAre.Font = New Font("Segoe UI", 10.0!, FontStyle.Bold)
        lblYouAre.ForeColor = Color.White
        pnlGame.Controls.Add(lblYouAre)

        lblInfo = New Label()
        lblInfo.Location = New Point(10, 30) : lblInfo.AutoSize = True
        lblInfo.Font = New Font("Segoe UI", 9.0!)
        lblInfo.ForeColor = Color.Yellow
        pnlGame.Controls.Add(lblInfo)

        boardPanel = New Panel()
        boardPanel.Location = New Point(10, 55)
        boardPanel.Size = New Size(BoardW, BoardH)
        boardPanel.BackColor = Color.Black
        AddHandler boardPanel.Paint, AddressOf BoardPanel_Paint
        pnlGame.Controls.Add(boardPanel)

        btnRestart = New Button() : btnRestart.Text = "Choi lai (Host)"
        btnRestart.Location = New Point(10, 55 + BoardH + 10)
        btnRestart.Size = New Size(150, 32)
        btnRestart.BackColor = Color.DimGray : btnRestart.ForeColor = Color.White
        btnRestart.FlatStyle = FlatStyle.Flat
        AddHandler btnRestart.Click, AddressOf BtnRestart_Click
        pnlGame.Controls.Add(btnRestart)

        lstLog = New ListBox()
        lstLog.Location = New Point(10, 55 + BoardH + 52)
        lstLog.Size = New Size(BoardW, 100)
        lstLog.BackColor = Color.FromArgb(20, 20, 20)
        lstLog.ForeColor = Color.LightGreen
        lstLog.Font = New Font("Consolas", 8.5!)
        pnlGame.Controls.Add(lstLog)

        Me.Controls.Add(pnlGame)
    End Sub

    ' ============================================================
    '  GDI BOARD
    ' ============================================================
    Private Sub BoardPanel_Paint(sender As Object, e As PaintEventArgs)
        If game Is Nothing Then Return
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim x As Integer, y As Integer

        For y = 0 To BombGame.ROWS - 1
            For x = 0 To BombGame.COLS - 1
                DrawCell(g, x, y)
            Next x
        Next y

        Dim i As Integer
        For i = 0 To game.Fires.Count - 1
            DrawFire(g, game.Fires(i).X, game.Fires(i).Y)
        Next i

        For i = 0 To game.Bombs.Count - 1
            DrawBomb(g, game.Bombs(i).X, game.Bombs(i).Y, game.Bombs(i).Timer)
        Next i

        If game.PlayerAlive(0) Then DrawPlayer(g, game.PlayerX(0), game.PlayerY(0), 0)
        If game.PlayerAlive(1) Then DrawPlayer(g, game.PlayerX(1), game.PlayerY(1), 1)
    End Sub

    Private Sub DrawCell(g As Graphics, x As Integer, y As Integer)
        Dim rx As Integer = x * CELL_SIZE
        Dim ry As Integer = y * CELL_SIZE
        Dim r As New Rectangle(rx, ry, CELL_SIZE, CELL_SIZE)

        Dim ct As BombGame.CellType = game.Map(x, y)
        Select Case ct
            Case BombGame.CellType.Wall
                Using br As New SolidBrush(Color.FromArgb(70, 70, 90))
                    g.FillRectangle(br, r)
                End Using
                Using p As New Pen(Color.FromArgb(110, 110, 130), 1)
                    g.DrawLine(p, rx, ry, rx + CELL_SIZE - 1, ry)
                    g.DrawLine(p, rx, ry, rx, ry + CELL_SIZE - 1)
                End Using
                Using p As New Pen(Color.FromArgb(40, 40, 55), 1)
                    g.DrawLine(p, rx + CELL_SIZE - 1, ry, rx + CELL_SIZE - 1, ry + CELL_SIZE - 1)
                    g.DrawLine(p, rx, ry + CELL_SIZE - 1, rx + CELL_SIZE - 1, ry + CELL_SIZE - 1)
                End Using
                Using p2 As New Pen(Color.FromArgb(55, 55, 75), 1)
                    g.DrawLine(p2, rx + 2, ry + CELL_SIZE \ 2, rx + CELL_SIZE - 2, ry + CELL_SIZE \ 2)
                    g.DrawLine(p2, rx + CELL_SIZE \ 2, ry + 2, rx + CELL_SIZE \ 2, ry + CELL_SIZE - 2)
                End Using

            Case BombGame.CellType.Box
                Using br As New SolidBrush(Color.FromArgb(140, 90, 40))
                    g.FillRectangle(br, r)
                End Using
                Using p As New Pen(Color.FromArgb(80, 50, 20), 2)
                    g.DrawRectangle(p, rx + 1, ry + 1, CELL_SIZE - 3, CELL_SIZE - 3)
                End Using
                Using p2 As New Pen(Color.FromArgb(100, 65, 25), 1)
                    g.DrawLine(p2, rx + 4, ry + 4, rx + CELL_SIZE - 4, ry + CELL_SIZE - 4)
                    g.DrawLine(p2, rx + CELL_SIZE - 4, ry + 4, rx + 4, ry + CELL_SIZE - 4)
                End Using
                Using br2 As New SolidBrush(Color.FromArgb(30, 200, 150, 80))
                    g.FillRectangle(br2, rx + 2, ry + 2, CELL_SIZE \ 3, CELL_SIZE \ 3)
                End Using

            Case Else
                Dim shade As Color = If((x + y) Mod 2 = 0,
                    Color.FromArgb(60, 60, 60),
                    Color.FromArgb(50, 50, 50))
                Using br As New SolidBrush(shade)
                    g.FillRectangle(br, r)
                End Using
        End Select
    End Sub

    Private Sub DrawBomb(g As Graphics, x As Integer, y As Integer, timer As Integer)
        Dim cx As Integer = x * CELL_SIZE + CELL_SIZE \ 2
        Dim cy As Integer = y * CELL_SIZE + CELL_SIZE \ 2
        Dim rad As Integer = CELL_SIZE \ 2 - 4

        Dim bombClr As Color = If(timer <= 2, Color.OrangeRed, Color.FromArgb(20, 20, 20))
        Using br As New SolidBrush(bombClr)
            g.FillEllipse(br, cx - rad, cy - rad, rad * 2, rad * 2)
        End Using
        Using p As New Pen(Color.FromArgb(180, 180, 180), 2)
            g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2)
        End Using
        Using br2 As New SolidBrush(Color.FromArgb(120, 255, 255, 255))
            g.FillEllipse(br2, cx - rad + 4, cy - rad + 3, rad \ 2, rad \ 3)
        End Using
        Dim fuse As Color = If(timer <= 2, Color.Yellow, Color.Orange)
        Using p2 As New Pen(fuse, 2)
            g.DrawLine(p2, cx + rad \ 2, cy - rad, cx + rad, cy - rad - 6)
        End Using
        Using br3 As New SolidBrush(Color.Yellow)
            g.FillEllipse(br3, cx + rad - 3, cy - rad - 9, 7, 7)
        End Using
        Using fnt As New Font("Segoe UI", 10.0!, FontStyle.Bold)
        Using brT As New SolidBrush(Color.White)
            Dim txt As String = timer.ToString()
            Dim sz As SizeF = g.MeasureString(txt, fnt)
            g.DrawString(txt, fnt, brT, CSng(cx - sz.Width / 2.0), CSng(cy - sz.Height / 2.0))
        End Using
        End Using
    End Sub

    Private Sub DrawFire(g As Graphics, x As Integer, y As Integer)
        Dim rx As Integer = x * CELL_SIZE
        Dim ry As Integer = y * CELL_SIZE
        Using br As New SolidBrush(Color.FromArgb(200, 255, 100, 0))
            g.FillRectangle(br, rx + 1, ry + 1, CELL_SIZE - 2, CELL_SIZE - 2)
        End Using
        Dim pts1() As PointF = {
            New PointF(rx + CELL_SIZE \ 2, ry + 2),
            New PointF(rx + 6, ry + CELL_SIZE - 4),
            New PointF(rx + CELL_SIZE - 6, ry + CELL_SIZE - 4)
        }
        Using br2 As New SolidBrush(Color.FromArgb(200, 255, 200, 0))
            g.FillPolygon(br2, pts1)
        End Using
        Dim pts2() As PointF = {
            New PointF(rx + CELL_SIZE \ 2, ry + 8),
            New PointF(rx + 10, ry + CELL_SIZE - 4),
            New PointF(rx + CELL_SIZE - 10, ry + CELL_SIZE - 4)
        }
        Using br3 As New SolidBrush(Color.FromArgb(220, 255, 255, 150))
            g.FillPolygon(br3, pts2)
        End Using
    End Sub

    Private Sub DrawPlayer(g As Graphics, x As Integer, y As Integer, player As Integer)
        Dim rx As Integer = x * CELL_SIZE + 3
        Dim ry As Integer = y * CELL_SIZE + 3
        Dim sz As Integer = CELL_SIZE - 6
        Dim bodyClr As Color = If(player = 0, Color.CornflowerBlue, Color.Tomato)
        Dim darkClr As Color = If(player = 0, Color.DarkBlue, Color.DarkRed)
        Using br As New SolidBrush(bodyClr)
            g.FillRectangle(br, rx + sz \ 4, ry + sz \ 3, sz \ 2, sz * 2 \ 3)
        End Using
        Dim headSz As Integer = sz * 5 \ 8
        Dim headX As Integer = rx + (sz - headSz) \ 2
        Dim headY As Integer = ry
        Using br As New SolidBrush(Color.Bisque)
            g.FillEllipse(br, headX, headY, headSz, headSz)
        End Using
        Using p As New Pen(darkClr, 2)
            g.DrawEllipse(p, headX, headY, headSz, headSz)
        End Using
        Using br As New SolidBrush(Color.Black)
            g.FillEllipse(br, headX + headSz \ 4 - 2, headY + headSz \ 3, 4, 4)
            g.FillEllipse(br, headX + headSz * 3 \ 4 - 2, headY + headSz \ 3, 4, 4)
        End Using
        Using fnt As New Font("Segoe UI", 7.0!, FontStyle.Bold)
        Using brT As New SolidBrush(If(player = 0, Color.Cyan, Color.Yellow))
            Dim txt As String = If(player = 0, "P1", "P2")
            Dim tsz As SizeF = g.MeasureString(txt, fnt)
            g.DrawString(txt, fnt, brT, CSng(rx + (sz - tsz.Width) / 2.0), CSng(ry + sz - tsz.Height - 1))
        End Using
        End Using
        If player = localPlayer Then
            Using p As New Pen(Color.Yellow, 2)
                g.DrawRectangle(p, x * CELL_SIZE + 1, y * CELL_SIZE + 1, CELL_SIZE - 3, CELL_SIZE - 3)
            End Using
        End If
    End Sub

    ' ============================================================
    '  KEYBOARD INPUT
    ' ============================================================
    Private Sub Form1_KeyDown(sender As Object, e As KeyEventArgs)
        If game Is Nothing OrElse game.GameOver Then Return
        If localPlayer < 0 Then Return

        Dim dx As Integer = 0, dy As Integer = 0
        Dim placeBomb As Boolean = False

        Select Case e.KeyCode
            Case Keys.W, Keys.Up : dy = -1
            Case Keys.S, Keys.Down : dy = 1
            Case Keys.A, Keys.Left : dx = -1
            Case Keys.D, Keys.Right : dx = 1
            Case Keys.Space : placeBomb = True
        End Select

        If placeBomb Then
            If isHost Then
                If game.TryPlaceBomb(localPlayer) Then
                    boardPanel.Invalidate()
                    ' FIX: danh dau gui STATE o Tick tiep theo, khong gui ngay
                    statePending = True
                    AppendLog(game.LastLog)
                End If
            Else
                peer.SendLine("BOMB:" & localPlayer.ToString())
            End If
            e.Handled = True
            Return
        End If

        If dx <> 0 OrElse dy <> 0 Then
            If isHost Then
                If game.TryMove(localPlayer, dx, dy) Then
                    boardPanel.Invalidate()
                    ' FIX: gop voi STATE cua Tick, khong spam lien tuc
                    statePending = True
                End If
            Else
                peer.SendLine("MOVE:" & localPlayer.ToString() & ":" & dx.ToString() & ":" & dy.ToString())
            End If
            e.Handled = True
        End If
    End Sub

    ' ============================================================
    '  TICK TIMER (chi host)
    ' ============================================================
    Private Sub TickTimer_Tick(sender As Object, e As EventArgs)
        If game Is Nothing OrElse Not isHost Then Return
        game.Tick()
        statePending = True  ' Tick luon gui STATE
        boardPanel.Invalidate()
        RefreshInfo()

        ' FIX: gui STATE 1 lan duy nhat sau khi Tick + input da xu ly
        If statePending Then
            BroadcastState()
            statePending = False
        End If

        If game.GameOver Then
            tickTimer.Stop()
            AppendLog(game.LastLog)
            ' FIX: dung BeginInvoke de tranh block network thread
            Me.BeginInvoke(New Action(Sub()
                MessageBox.Show(game.LastLog, "Ket thuc!")
            End Sub))
        End If
    End Sub

    ' ============================================================
    '  NETWORK
    ' ============================================================
    Private Sub BtnHost_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        isHost = True : localPlayer = -1
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        Try
            peer.StartHost(port)
            lblStatus.Text = "Dang cho doi thu tren port " & port.ToString() & "..."
        Catch ex As Exception
            MessageBox.Show("Loi: " & ex.Message)
        End Try
    End Sub

    Private Sub BtnJoin_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        If txtIP.Text.Trim() = "" Then MessageBox.Show("Nhap IP.") : Return
        isHost = False : localPlayer = 1
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        lblStatus.Text = "Dang ket noi..."
        peer.ConnectToHost(txtIP.Text.Trim(), port)
    End Sub

    Private Sub Peer_Connected()
        If Not isHost Then peer.SendLine("HELLO:Client")
    End Sub

    Private Sub Peer_Disconnected()
        tickTimer.Stop()
        ' FIX: dung BeginInvoke de tranh deadlock khi MessageBox block UI thread
        Me.BeginInvoke(New Action(Sub()
            MessageBox.Show("Mat ket noi.")
            pnlGame.Visible = False : pnlConnect.Visible = True
        End Sub))
    End Sub

    Private Sub Peer_LineReceived(line As String)
        If line.StartsWith("HELLO") Then
            If isHost Then
                localPlayer = 0
                game = New BombGame()
                ShowGamePanel()
                statePending = False
                BroadcastState()
                tickTimer.Start()
                AppendLog("Doi thu vao phong. Ban la Player 1 (xanh). WASD+Space de choi.")
            End If

        ElseIf line.StartsWith("STATE:") Then
            If game Is Nothing Then game = New BombGame()
            game.Deserialize(line.Substring(6))
            If Not pnlGame.Visible Then ShowGamePanel()
            boardPanel.Invalidate()
            RefreshInfo()
            If game.GameOver Then
                AppendLog(game.LastLog)
                Me.BeginInvoke(New Action(Sub()
                    MessageBox.Show(game.LastLog, "Ket thuc!")
                End Sub))
            End If

        ElseIf line.StartsWith("MOVE:") Then
            If isHost Then
                Dim parts As String() = line.Substring(5).Split(":"c)
                If parts.Length >= 3 Then
                    Dim p, dx, dy As Integer
                    Integer.TryParse(parts(0), p)
                    Integer.TryParse(parts(1), dx)
                    Integer.TryParse(parts(2), dy)
                    If game.TryMove(p, dx, dy) Then
                        boardPanel.Invalidate()
                        BroadcastState()
                    End If
                End If
            End If

        ElseIf line.StartsWith("BOMB:") Then
            If isHost Then
                Dim p As Integer
                Integer.TryParse(line.Substring(5), p)
                If game.TryPlaceBomb(p) Then
                    boardPanel.Invalidate()
                    BroadcastState()
                    AppendLog(game.LastLog)
                End If
            End If
        End If
    End Sub

    Private Sub ShowGamePanel()
        pnlConnect.Visible = False : pnlGame.Visible = True
        lblYouAre.Text = If(localPlayer = 0,
            "Ban la: Player 1 (Xanh)  |  WASD/Mui ten di chuyen, Space dat bom",
            "Ban la: Player 2 (Do)    |  WASD/Mui ten di chuyen, Space dat bom")
        RefreshInfo()
    End Sub

    Private Sub BroadcastState()
        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("STATE:" & game.Serialize())
        End If
    End Sub

    Private Sub BtnRestart_Click(sender As Object, e As EventArgs)
        If Not isHost OrElse game Is Nothing Then Return
        tickTimer.Stop()
        game.ResetBoard()
        statePending = False
        boardPanel.Invalidate()
        RefreshInfo()
        BroadcastState()
        AppendLog("Bat dau lai!")
        tickTimer.Start()
    End Sub

    Private Sub RefreshInfo()
        If game Is Nothing Then Return
        Dim sb As New System.Text.StringBuilder()
        sb.Append("P1 ")
        sb.Append(If(game.PlayerAlive(0), "[Song]", "[Chet]"))
        sb.Append("  |  P2 ")
        sb.Append(If(game.PlayerAlive(1), "[Song]", "[Chet]"))
        sb.Append("  |  Bom tren san: ")
        sb.Append(game.Bombs.Count.ToString())
        sb.Append("  |  Lua: ")
        sb.Append(game.Fires.Count.ToString())
        If game.GameOver Then
            sb.Append("  >>> " & game.LastLog)
        End If
        lblInfo.Text = sb.ToString()
    End Sub

    Private Sub AppendLog(msg As String)
        lstLog.Items.Add(msg)
        lstLog.TopIndex = lstLog.Items.Count - 1
    End Sub

End Class
