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
    Private isPvAIMode As Boolean = False

    Private BoardW As Integer = BombGame.COLS * CELL_SIZE
    Private BoardH As Integer = BombGame.ROWS * CELL_SIZE

    ' === UI mode select ===
    Private pnlMode As Panel

    ' === UI connect (PvP) ===
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

    ' === Chat (PvP only) ===
    Private pnlChat As Panel
    Private lstChat As ListBox
    Private txtChatInput As TextBox
    Private btnSend As Button
    Private Const CHAT_W As Integer = 210

    ' === Timer ===
    Private tickTimer As System.Windows.Forms.Timer
    Private statePending As Boolean = False

    Public Sub New()
        InitUI()
    End Sub

    Private Sub InitUI()
        Me.Text = "Dat Bom - 2CongLC"
        Me.ClientSize = New Size(BoardW + 20 + CHAT_W, BoardH + 220)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(30, 30, 30)
        Me.KeyPreview = True
        AddHandler Me.KeyDown, AddressOf Form1_KeyDown

        tickTimer = New System.Windows.Forms.Timer()
        tickTimer.Interval = TICK_MS
        AddHandler tickTimer.Tick, AddressOf TickTimer_Tick

        BuildModePanel()
        BuildConnectPanel()
        BuildGamePanel()
        BuildChatPanel()
        pnlConnect.Visible = False
        pnlGame.Visible = False
    End Sub

    ' ============================================================
    '  MODE SELECT PANEL
    ' ============================================================
    Private Sub BuildModePanel()
        pnlMode = New Panel()
        pnlMode.Dock = DockStyle.Fill
        pnlMode.BackColor = Color.FromArgb(30, 30, 30)

        Dim lbl As New Label()
        lbl.Text = "DAT BOM ONLINE"
        lbl.Font = New Font("Segoe UI", 24.0!, FontStyle.Bold)
        lbl.ForeColor = Color.OrangeRed
        lbl.Location = New Point(200, 80) : lbl.AutoSize = True
        pnlMode.Controls.Add(lbl)

        Dim lbl2 As New Label()
        lbl2.Text = "Chon che do choi:"
        lbl2.Font = New Font("Segoe UI", 13.0!)
        lbl2.ForeColor = Color.LightGray
        lbl2.Location = New Point(265, 155) : lbl2.AutoSize = True
        pnlMode.Controls.Add(lbl2)

        ' PvP button
        Dim btnPvP As New Button()
        btnPvP.Text = "⚔  PvP - 2 Nguoi (LAN)"
        btnPvP.Font = New Font("Segoe UI", 12.0!, FontStyle.Bold)
        btnPvP.Location = New Point(215, 200) : btnPvP.Size = New Size(300, 60)
        btnPvP.BackColor = Color.SteelBlue : btnPvP.ForeColor = Color.White
        btnPvP.FlatStyle = FlatStyle.Flat
        AddHandler btnPvP.Click, AddressOf BtnPvP_Click
        pnlMode.Controls.Add(btnPvP)

        ' PvAI button
        Dim btnPvAI As New Button()
        btnPvAI.Text = "👾  PvAI - Danh voi Monster"
        btnPvAI.Font = New Font("Segoe UI", 12.0!, FontStyle.Bold)
        btnPvAI.Location = New Point(215, 280) : btnPvAI.Size = New Size(300, 60)
        btnPvAI.BackColor = Color.FromArgb(160, 50, 200) : btnPvAI.ForeColor = Color.White
        btnPvAI.FlatStyle = FlatStyle.Flat
        AddHandler btnPvAI.Click, AddressOf BtnPvAI_Click
        pnlMode.Controls.Add(btnPvAI)

        Dim lHelp As New Label()
        lHelp.Text = "Dieu khien: WASD / Mui ten di chuyen  |  Space dat bom"
        lHelp.ForeColor = Color.Yellow
        lHelp.Font = New Font("Segoe UI", 9.0!)
        lHelp.Location = New Point(175, 375) : lHelp.AutoSize = True
        pnlMode.Controls.Add(lHelp)

        Me.Controls.Add(pnlMode)
    End Sub

    Private Sub BtnPvP_Click(sender As Object, e As EventArgs)
        isPvAIMode = False
        pnlMode.Visible = False
        pnlConnect.Visible = True
    End Sub

    Private Sub BtnPvAI_Click(sender As Object, e As EventArgs)
        isPvAIMode = True
        pnlMode.Visible = False
        StartPvAI()
    End Sub

    Private Sub StartPvAI()
        isHost = True
        localPlayer = 0
        game = New BombGame()
        game.IsPvAI = True
        game.SpawnMonsters(4)
        statePending = False
        ShowGamePanel()
        AppendLog("PvAI: Tieu diet het 4 monster de thang!")
        tickTimer.Start()
    End Sub

    ' ============================================================
    '  CONNECT PANEL (PvP)
    ' ============================================================
    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Dock = DockStyle.Fill
        pnlConnect.BackColor = Color.FromArgb(30, 30, 30)

        Dim lbl As New Label()
        lbl.Text = "PvP - Ket Noi LAN"
        lbl.Font = New Font("Segoe UI", 20.0!, FontStyle.Bold)
        lbl.ForeColor = Color.SteelBlue
        lbl.Location = New Point(240, 70) : lbl.AutoSize = True
        pnlConnect.Controls.Add(lbl)

        Dim btnBack As New Button()
        btnBack.Text = "< Quay lai"
        btnBack.Location = New Point(10, 10) : btnBack.Size = New Size(100, 30)
        btnBack.BackColor = Color.DimGray : btnBack.ForeColor = Color.White
        btnBack.FlatStyle = FlatStyle.Flat
        AddHandler btnBack.Click, Sub(s, ev)
            pnlConnect.Visible = False
            pnlMode.Visible = True
        End Sub
        pnlConnect.Controls.Add(btnBack)

        Dim lPort As New Label() : lPort.Text = "Port:" : lPort.ForeColor = Color.White
        lPort.Location = New Point(300, 150) : lPort.AutoSize = True
        pnlConnect.Controls.Add(lPort)
        txtPort = New TextBox() : txtPort.Text = DEFAULT_PORT.ToString()
        txtPort.Location = New Point(355, 147) : txtPort.Width = 80
        pnlConnect.Controls.Add(txtPort)

        btnHost = New Button() : btnHost.Text = "Tao phong (Host)"
        btnHost.Location = New Point(300, 185) : btnHost.Size = New Size(200, 38)
        btnHost.BackColor = Color.OrangeRed : btnHost.ForeColor = Color.White
        btnHost.FlatStyle = FlatStyle.Flat
        AddHandler btnHost.Click, AddressOf BtnHost_Click
        pnlConnect.Controls.Add(btnHost)

        Dim lIP As New Label() : lIP.Text = "IP Host:" : lIP.ForeColor = Color.White
        lIP.Location = New Point(300, 245) : lIP.AutoSize = True
        pnlConnect.Controls.Add(lIP)
        txtIP = New TextBox() : txtIP.Text = "127.0.0.1"
        txtIP.Location = New Point(370, 242) : txtIP.Width = 140
        pnlConnect.Controls.Add(txtIP)

        btnJoin = New Button() : btnJoin.Text = "Vao phong (Join)"
        btnJoin.Location = New Point(300, 277) : btnJoin.Size = New Size(200, 38)
        btnJoin.BackColor = Color.SteelBlue : btnJoin.ForeColor = Color.White
        btnJoin.FlatStyle = FlatStyle.Flat
        AddHandler btnJoin.Click, AddressOf BtnJoin_Click
        pnlConnect.Controls.Add(btnJoin)

        lblStatus = New Label() : lblStatus.Location = New Point(255, 340) : lblStatus.AutoSize = True
        lblStatus.ForeColor = Color.LightGray
        lblStatus.Text = "Host: bam 'Tao phong'." & Environment.NewLine & "Khach: nhap IP roi bam 'Vao phong'."
        pnlConnect.Controls.Add(lblStatus)

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

        boardPanel = New DoubleBufferedPanel()
        boardPanel.Location = New Point(10, 55)
        boardPanel.Size = New Size(BoardW, BoardH)
        boardPanel.BackColor = Color.Black
        AddHandler boardPanel.Paint, AddressOf BoardPanel_Paint
        pnlGame.Controls.Add(boardPanel)

        btnRestart = New Button() : btnRestart.Text = "Choi lai"
        btnRestart.Location = New Point(10, 55 + BoardH + 10)
        btnRestart.Size = New Size(110, 32)
        btnRestart.BackColor = Color.DimGray : btnRestart.ForeColor = Color.White
        btnRestart.FlatStyle = FlatStyle.Flat
        AddHandler btnRestart.Click, AddressOf BtnRestart_Click
        pnlGame.Controls.Add(btnRestart)

        Dim btnMenu As New Button() : btnMenu.Text = "Menu chinh"
        btnMenu.Location = New Point(130, 55 + BoardH + 10)
        btnMenu.Size = New Size(110, 32)
        btnMenu.BackColor = Color.DimGray : btnMenu.ForeColor = Color.White
        btnMenu.FlatStyle = FlatStyle.Flat
        AddHandler btnMenu.Click, Sub(s, ev)
            tickTimer.Stop()
            If peer IsNot Nothing Then peer.CloseConnection()
            peer = Nothing
            game = Nothing
            pnlGame.Visible = False
            pnlConnect.Visible = False
            pnlMode.Visible = True
        End Sub
        pnlGame.Controls.Add(btnMenu)

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
    '  CHAT PANEL (PvP only, nam ben phai board)
    ' ============================================================
    Private Sub BuildChatPanel()
        Dim chatX As Integer = BoardW + 20
        Dim chatH As Integer = BoardH + 220

        pnlChat = New Panel()
        pnlChat.Location = New Point(chatX, 0)
        pnlChat.Size = New Size(CHAT_W, chatH)
        pnlChat.BackColor = Color.FromArgb(22, 22, 30)
        pnlChat.Visible = False

        Dim lblChat As New Label()
        lblChat.Text = "CHAT"
        lblChat.Font = New Font("Segoe UI", 10.0!, FontStyle.Bold)
        lblChat.ForeColor = Color.SteelBlue
        lblChat.Location = New Point(8, 8) : lblChat.AutoSize = True
        pnlChat.Controls.Add(lblChat)

        Dim sep As New Label()
        sep.BackColor = Color.SteelBlue
        sep.Location = New Point(0, 28) : sep.Size = New Size(CHAT_W, 1)
        pnlChat.Controls.Add(sep)

        lstChat = New ListBox()
        lstChat.Location = New Point(4, 34)
        lstChat.Size = New Size(CHAT_W - 8, chatH - 34 - 60)
        lstChat.BackColor = Color.FromArgb(18, 18, 26)
        lstChat.ForeColor = Color.LightCyan
        lstChat.Font = New Font("Segoe UI", 8.5!)
        lstChat.BorderStyle = BorderStyle.None
        lstChat.HorizontalScrollbar = False
        lstChat.ScrollAlwaysVisible = False
        pnlChat.Controls.Add(lstChat)

        txtChatInput = New TextBox()
        txtChatInput.Location = New Point(4, chatH - 54)
        txtChatInput.Size = New Size(CHAT_W - 8, 24)
        txtChatInput.BackColor = Color.FromArgb(40, 40, 55)
        txtChatInput.ForeColor = Color.White
        txtChatInput.Font = New Font("Segoe UI", 9.0!)
        txtChatInput.BorderStyle = BorderStyle.FixedSingle
        txtChatInput.MaxLength = 80
        ' Enter gui tin nhan
        AddHandler txtChatInput.KeyDown, Sub(s, ev)
            If ev.KeyCode = Keys.Enter Then
                ev.SuppressKeyPress = True
                SendChat()
            End If
        End Sub
        pnlChat.Controls.Add(txtChatInput)

        btnSend = New Button()
        btnSend.Text = "Gui"
        btnSend.Location = New Point(4, chatH - 26)
        btnSend.Size = New Size(CHAT_W - 8, 22)
        btnSend.BackColor = Color.SteelBlue : btnSend.ForeColor = Color.White
        btnSend.FlatStyle = FlatStyle.Flat
        btnSend.Font = New Font("Segoe UI", 8.5!)
        AddHandler btnSend.Click, Sub(s, ev) SendChat()
        pnlChat.Controls.Add(btnSend)

        Me.Controls.Add(pnlChat)
    End Sub

    Private Sub SendChat()
        Dim msg As String = txtChatInput.Text.Trim()
        If msg = "" OrElse peer Is Nothing Then Return
        Dim tag As String = If(localPlayer = 0, "P1", "P2")
        AppendChat(tag & ": " & msg)
        peer.SendLine("CHAT:" & tag & ":" & msg.Replace(":", " "))
        txtChatInput.Clear()
    End Sub

    Private Sub AppendChat(msg As String)
        lstChat.Items.Add(msg)
        lstChat.TopIndex = lstChat.Items.Count - 1
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
        For i = 0 To game.Powerups.Count - 1
            DrawPowerup(g, game.Powerups(i).X, game.Powerups(i).Y, game.Powerups(i).Kind)
        Next i

        For i = 0 To game.Fires.Count - 1
            DrawFire(g, game.Fires(i).X, game.Fires(i).Y)
        Next i

        For i = 0 To game.Bombs.Count - 1
            DrawBomb(g, game.Bombs(i).X, game.Bombs(i).Y, game.Bombs(i).Timer)
        Next i

        ' Ve monster
        For i = 0 To game.Monsters.Count - 1
            If game.Monsters(i).Alive Then
                DrawMonster(g, game.Monsters(i).X, game.Monsters(i).Y)
            End If
        Next i

        If game.PlayerAlive(0) Then DrawPlayer(g, game.PlayerX(0), game.PlayerY(0), 0)
        If Not game.IsPvAI AndAlso game.PlayerAlive(1) Then DrawPlayer(g, game.PlayerX(1), game.PlayerY(1), 1)
    End Sub

    Private Sub DrawCell(g As Graphics, x As Integer, y As Integer)
        Dim rx As Integer = x * CELL_SIZE
        Dim ry As Integer = y * CELL_SIZE
        Dim r As New Rectangle(rx, ry, CELL_SIZE, CELL_SIZE)

        Select Case game.Map(x, y)
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
            Case Else
                Dim shade As Color = If((x + y) Mod 2 = 0, Color.FromArgb(60, 60, 60), Color.FromArgb(50, 50, 50))
                Using br As New SolidBrush(shade)
                    g.FillRectangle(br, r)
                End Using
        End Select
    End Sub

    Private Sub DrawPowerup(g As Graphics, x As Integer, y As Integer, kind As BombGame.PowerupType)
        Dim rx As Integer = x * CELL_SIZE + 6
        Dim ry As Integer = y * CELL_SIZE + 6
        Dim sz As Integer = CELL_SIZE - 12
        Dim bgColor As Color
        Dim symbol As String
        Select Case kind
            Case BombGame.PowerupType.Range
                bgColor = Color.FromArgb(220, 30, 180, 255) : symbol = "R"
            Case BombGame.PowerupType.BombUp
                bgColor = Color.FromArgb(220, 255, 80, 30) : symbol = "B"
            Case Else
                bgColor = Color.FromArgb(220, 50, 220, 80) : symbol = "S"
        End Select
        Using br As New SolidBrush(bgColor)
            g.FillRectangle(br, rx, ry, sz, sz)
        End Using
        Using p As New Pen(Color.White, 1.5!)
            g.DrawRectangle(p, rx, ry, sz, sz)
        End Using
        Using fnt As New Font("Arial", 10.0!, FontStyle.Bold)
            Using tbr As New SolidBrush(Color.White)
                Dim sf As New StringFormat()
                sf.Alignment = StringAlignment.Center
                sf.LineAlignment = StringAlignment.Center
                g.DrawString(symbol, fnt, tbr, New RectangleF(rx, ry, sz, sz), sf)
            End Using
        End Using
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
        Using fnt As New Font("Arial", 8.0!, FontStyle.Bold)
            Using tbr As New SolidBrush(Color.White)
                Dim sf As New StringFormat()
                sf.Alignment = StringAlignment.Center : sf.LineAlignment = StringAlignment.Center
                g.DrawString(timer.ToString(), fnt, tbr, New RectangleF(cx - rad, cy - rad, rad * 2, rad * 2), sf)
            End Using
        End Using
    End Sub

    Private Sub DrawFire(g As Graphics, x As Integer, y As Integer)
        Dim rx As Integer = x * CELL_SIZE + 2
        Dim ry As Integer = y * CELL_SIZE + 2
        Dim sz As Integer = CELL_SIZE - 4
        Using br As New SolidBrush(Color.FromArgb(200, 255, 80, 0))
            g.FillRectangle(br, rx, ry, sz, sz)
        End Using
        Using br2 As New SolidBrush(Color.FromArgb(150, 255, 220, 0))
            g.FillRectangle(br2, rx + 6, ry + 6, sz - 12, sz - 12)
        End Using
    End Sub

    Private Sub DrawPlayer(g As Graphics, x As Integer, y As Integer, player As Integer)
        Dim cx As Integer = x * CELL_SIZE + CELL_SIZE \ 2
        Dim top As Integer = y * CELL_SIZE + 3

        ' Mau ao theo player
        Dim bodyClr As Color = If(player = 0, Color.DodgerBlue, Color.OrangeRed)
        Dim darkClr As Color = If(player = 0, Color.FromArgb(20, 90, 180), Color.FromArgb(180, 60, 10))
        Dim skinClr As Color = Color.FromArgb(255, 220, 170)
        Dim skinDark As Color = Color.FromArgb(210, 170, 120)

        ' --- Chan (2 hinh chu nhat nho phia duoi) ---
        Dim legW As Integer = 7
        Dim legH As Integer = 8
        Dim legY As Integer = top + 30
        Using br As New SolidBrush(darkClr)
            g.FillRectangle(br, cx - 9, legY, legW, legH)
            g.FillRectangle(br, cx + 2, legY, legW, legH)
        End Using
        ' Giay (den)
        Using br As New SolidBrush(Color.FromArgb(40, 40, 40))
            g.FillRectangle(br, cx - 10, legY + legH - 3, legW + 2, 4)
            g.FillRectangle(br, cx + 1, legY + legH - 3, legW + 2, 4)
        End Using

        ' --- Than (hinh chu nhat bo goc) ---
        Dim bodyRect As New Rectangle(cx - 11, top + 16, 22, 16)
        Using br As New SolidBrush(bodyClr)
            g.FillRectangle(br, bodyRect)
        End Using
        ' Vien than
        Using p As New Pen(darkClr, 1.5!)
            g.DrawRectangle(p, bodyRect)
        End Using
        ' Cuc ao (dot nho)
        Using br As New SolidBrush(Color.White)
            g.FillEllipse(br, cx - 2, top + 19, 4, 4)
        End Using

        ' --- Tay (2 hinh chu nhat hai ben) ---
        Using br As New SolidBrush(bodyClr)
            g.FillRectangle(br, cx - 17, top + 17, 6, 11)  ' tay trai
            g.FillRectangle(br, cx + 11, top + 17, 6, 11)  ' tay phai
        End Using
        ' Ban tay
        Using br As New SolidBrush(skinClr)
            g.FillEllipse(br, cx - 18, top + 26, 7, 7)
            g.FillEllipse(br, cx + 11, top + 26, 7, 7)
        End Using

        ' --- Co (hinh chu nhat nho) ---
        Using br As New SolidBrush(skinClr)
            g.FillRectangle(br, cx - 4, top + 11, 8, 6)
        End Using

        ' --- Dau (hinh tron) ---
        Dim headRad As Integer = 10
        Using br As New SolidBrush(skinClr)
            g.FillEllipse(br, cx - headRad, top, headRad * 2, headRad * 2)
        End Using
        ' Vien dau
        Using p As New Pen(skinDark, 1.0!)
            g.DrawEllipse(p, cx - headRad, top, headRad * 2, headRad * 2)
        End Using

        ' --- Mu bao hiem / non (nua hinh tron phia tren dau) ---
        Dim hatClr As Color = If(player = 0, Color.FromArgb(0, 60, 160), Color.FromArgb(160, 30, 0))
        Using br As New SolidBrush(hatClr)
            g.FillEllipse(br, cx - headRad - 1, top - 3, (headRad + 1) * 2, headRad + 4)
        End Using
        ' Vanh mu
        Using br As New SolidBrush(hatClr)
            g.FillRectangle(br, cx - headRad - 3, top + 5, (headRad + 3) * 2, 3)
        End Using

        ' --- Mat (2 diem den) ---
        Using br As New SolidBrush(Color.FromArgb(30, 30, 30))
            g.FillEllipse(br, cx - 5, top + 5, 3, 3)
            g.FillEllipse(br, cx + 2, top + 5, 3, 3)
        End Using
        ' Mieng (duong cong nho) ---
        Using p As New Pen(Color.FromArgb(160, 80, 60), 1.2!)
            g.DrawArc(p, cx - 4, top + 9, 8, 4, 10, 160)
        End Using

        ' --- So hieu P1/P2 tren mu ---
        Using fnt As New Font("Arial", 6.5!, FontStyle.Bold)
            Using tbr As New SolidBrush(Color.White)
                Dim sf As New StringFormat()
                sf.Alignment = StringAlignment.Center
                sf.LineAlignment = StringAlignment.Center
                g.DrawString(If(player = 0, "1", "2"), fnt, tbr,
                    New RectangleF(cx - 6, top - 4, 12, 10), sf)
            End Using
        End Using
    End Sub

    Private Sub DrawMonster(g As Graphics, x As Integer, y As Integer)
        Dim cx As Integer = x * CELL_SIZE + CELL_SIZE \ 2
        Dim cy As Integer = y * CELL_SIZE + CELL_SIZE \ 2
        Dim rad As Integer = CELL_SIZE \ 2 - 6

        ' Than monster mau xanh la cay
        Using br As New SolidBrush(Color.FromArgb(50, 200, 80))
            g.FillEllipse(br, cx - rad, cy - rad + 4, rad * 2, rad * 2 - 2)
        End Using
        ' Dau
        Using br As New SolidBrush(Color.FromArgb(60, 220, 90))
            g.FillEllipse(br, cx - rad + 3, cy - rad - 2, rad * 2 - 6, rad + 4)
        End Using
        ' Mat do
        Using br As New SolidBrush(Color.Red)
            g.FillEllipse(br, cx - 7, cy - rad + 1, 5, 5)
            g.FillEllipse(br, cx + 2, cy - rad + 1, 5, 5)
        End Using
        ' Vien
        Using p As New Pen(Color.FromArgb(20, 120, 40), 1.5!)
            g.DrawEllipse(p, cx - rad, cy - rad + 4, rad * 2, rad * 2 - 2)
        End Using
    End Sub

    ' ============================================================
    '  INPUT
    ' ============================================================
    Private Sub Form1_KeyDown(sender As Object, e As KeyEventArgs)
        If game Is Nothing OrElse localPlayer < 0 Then Return
        ' Neu dang focus vao chat thi khong xu ly phim game
        If txtChatInput IsNot Nothing AndAlso txtChatInput.Focused Then Return

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
                    statePending = True
                End If
            Else
                peer.SendLine("MOVE:" & localPlayer.ToString() & ":" & dx.ToString() & ":" & dy.ToString())
            End If
            e.Handled = True
        End If
    End Sub

    ' ============================================================
    '  TICK TIMER
    ' ============================================================
    Private Sub TickTimer_Tick(sender As Object, e As EventArgs)
        If game Is Nothing OrElse Not isHost Then Return
        game.Tick()
        statePending = True
        boardPanel.Invalidate()
        RefreshInfo()

        If statePending AndAlso Not isPvAIMode Then
            BroadcastState()
            statePending = False
        End If

        If game.GameOver Then
            tickTimer.Stop()
            AppendLog(game.LastLog)
            Me.BeginInvoke(New Action(Sub()
                MessageBox.Show(game.LastLog, "Ket thuc!")
            End Sub))
        End If
    End Sub

    ' ============================================================
    '  NETWORK (PvP only)
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
        Me.BeginInvoke(New Action(Sub()
            If game IsNot Nothing AndAlso game.GameOver Then Return
            MessageBox.Show("Mat ket noi.")
            pnlGame.Visible = False
            pnlConnect.Visible = False
            pnlMode.Visible = True
        End Sub))
    End Sub

    Private Sub Peer_LineReceived(line As String)
        If line.StartsWith("HELLO") Then
            If isHost Then
                localPlayer = 0
                game = New BombGame()
                game.IsPvAI = False
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
        ElseIf line.StartsWith("CHAT:") Then
            Dim payload As String = line.Substring(5)
            Dim colon As Integer = payload.IndexOf(":"c)
            If colon >= 0 Then
                Dim tag As String = payload.Substring(0, colon)
                Dim msg As String = payload.Substring(colon + 1)
                AppendChat(tag & ": " & msg)
            End If

        End If
    End Sub

    Private Sub ShowGamePanel()
        pnlConnect.Visible = False
        pnlMode.Visible = False
        pnlGame.Visible = True
        pnlChat.Visible = Not isPvAIMode
        lstChat.Items.Clear()
        If isPvAIMode Then
            lblYouAre.Text = "PvAI  |  WASD/Mui ten di chuyen, Space dat bom  |  Tieu diet het monster!"
        Else
            lblYouAre.Text = If(localPlayer = 0,
                "Ban la: Player 1 (Xanh)  |  WASD/Mui ten di chuyen, Space dat bom",
                "Ban la: Player 2 (Do)    |  WASD/Mui ten di chuyen, Space dat bom")
        End If
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
        If isPvAIMode Then
            game.IsPvAI = True
            game.SpawnMonsters(4)
        End If
        statePending = False
        boardPanel.Invalidate()
        RefreshInfo()
        If Not isPvAIMode Then BroadcastState()
        AppendLog("Bat dau lai!")
        tickTimer.Start()
    End Sub

    Private Sub RefreshInfo()
        If game Is Nothing Then Return
        Dim sb As New System.Text.StringBuilder()
        If isPvAIMode Then
            sb.Append("Player ")
            sb.Append(If(game.PlayerAlive(0), "[Song]", "[Chet]"))
            sb.Append(String.Format("  R:{0} B:{1}{2}", game.PlayerRange(0), game.PlayerMaxBombs(0),
                If(game.PlayerSpeed(0) > 0, " SPD", "")))
            sb.Append(String.Format("  |  Monster con lai: {0}", game.CountAliveMonsters()))
        Else
            sb.Append("P1 ")
            sb.Append(If(game.PlayerAlive(0), "[Song]", "[Chet]"))
            sb.Append(String.Format(" R:{0} B:{1}{2}", game.PlayerRange(0), game.PlayerMaxBombs(0),
                If(game.PlayerSpeed(0) > 0, " SPD", "")))
            sb.Append("  |  P2 ")
            sb.Append(If(game.PlayerAlive(1), "[Song]", "[Chet]"))
            sb.Append(String.Format(" R:{0} B:{1}{2}", game.PlayerRange(1), game.PlayerMaxBombs(1),
                If(game.PlayerSpeed(1) > 0, " SPD", "")))
        End If
        If game.GameOver Then sb.Append("  >>> " & game.LastLog)
        lblInfo.Text = sb.ToString()
    End Sub

    Private Sub AppendLog(msg As String)
        lstLog.Items.Add(msg)
        lstLog.TopIndex = lstLog.Items.Count - 1
    End Sub

End Class

' Panel co double buffering de chong nhay man hinh
Public Class DoubleBufferedPanel
    Inherits Panel
    Public Sub New()
        Me.SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                    ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.UserPaint, True)
        Me.UpdateStyles()
    End Sub
End Class
