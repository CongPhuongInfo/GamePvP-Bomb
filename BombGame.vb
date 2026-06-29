Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic

''' <summary>
''' Logic game dat bom kieu Bomberman cho 2 nguoi choi qua mang.
''' Ban do 13x11. Moi o la: Empty, Wall (khong pha duoc), Box (pha duoc), Bomb, Fire.
''' Player 1 (Host) bat dau goc tren-trai, Player 2 (Khach) goc duoi-phai.
''' Protocol: moi nuoc di la MOVE:player:dx:dy hoac BOMB:player
''' Host tinh toan tat ca, gui STATE xuong ca 2.
''' </summary>
Public Class BombGame

    Public Const COLS As Integer = 13
    Public Const ROWS As Integer = 11
    Public Const MAX_BOMBS As Integer = 1    ' moi nguoi duoc dat 1 bom cung luc
    Public Const BOMB_TIMER As Integer = 5   ' so tick den khi no (moi tick = 500ms)
    Public Const FIRE_DURATION As Integer = 3 ' so tick lua con chay

    Public Enum CellType As Byte
        Empty = 0
        Wall = 1    ' tuong co dinh, khong pha duoc
        Box = 2     ' thung go, co the pha bang bom
    End Enum

    Public Structure BombInfo
        Public X As Integer
        Public Y As Integer
        Public Owner As Integer ' 0 hoac 1
        Public Timer As Integer ' dem nguoc, khi = 0 thi no
        Public Range As Integer ' pham vi lua (o tinh tu tam)
    End Structure

    Public Structure FireCell
        Public X As Integer
        Public Y As Integer
        Public Timer As Integer ' dem nguoc, khi = 0 thi tat lua
    End Structure

    ' Trang thai ban do
    Public Map(COLS - 1, ROWS - 1) As CellType

    ' Vi tri nguoi choi (o)
    Public PlayerX(1) As Integer
    Public PlayerY(1) As Integer
    Public PlayerAlive(1) As Boolean
    Public PlayerBombCount(1) As Integer ' so bom dang dat tren san

    ' Danh sach bom dang tich tac
    Public Bombs As New List(Of BombInfo)()

    ' Danh sach o dang chay lua
    Public Fires As New List(Of FireCell)()

    Public GameOver As Boolean
    Public Winner As Integer  ' -1=hoa, 0=P1, 1=P2
    Public LastLog As String
    Public TickCount As Integer

    Public Sub New()
        ResetBoard()
    End Sub

    Public Sub ResetBoard()
        Dim x As Integer, y As Integer

        ' Xay tuong va thung
        For y = 0 To ROWS - 1
            For x = 0 To COLS - 1
                If x = 0 OrElse x = COLS - 1 OrElse y = 0 OrElse y = ROWS - 1 Then
                    Map(x, y) = CellType.Wall  ' vien ngoai
                ElseIf x Mod 2 = 0 AndAlso y Mod 2 = 0 Then
                    Map(x, y) = CellType.Wall  ' tuong co dinh ben trong (luoi cheo)
                Else
                    Map(x, y) = CellType.Empty
                End If
            Next x
        Next y

        ' Dat thung go ngau nhien (tranh goc xuat phat)
        Dim rng As New Random(42)
        For y = 1 To ROWS - 2
            For x = 1 To COLS - 2
                If Map(x, y) = CellType.Empty Then
                    ' Bo trong goc 2x2 cho moi nguoi choi
                    Dim safeP1 As Boolean = (x <= 2 AndAlso y <= 2)
                    Dim safeP2 As Boolean = (x >= COLS - 3 AndAlso y >= ROWS - 3)
                    If Not safeP1 AndAlso Not safeP2 Then
                        If rng.Next(100) < 65 Then
                            Map(x, y) = CellType.Box
                        End If
                    End If
                End If
            Next x
        Next y

        ' Vi tri xuat phat
        PlayerX(0) = 1 : PlayerY(0) = 1
        PlayerX(1) = COLS - 2 : PlayerY(1) = ROWS - 2
        PlayerAlive(0) = True
        PlayerAlive(1) = True
        PlayerBombCount(0) = 0
        PlayerBombCount(1) = 0

        Bombs.Clear()
        Fires.Clear()
        GameOver = False
        Winner = -1
        LastLog = "Bat dau! WASD/Mui ten di chuyen, Space dat bom."
        TickCount = 0
    End Sub

    ''' <summary>Di chuyen nguoi choi. dx,dy la -1/0/1. Tra ve True neu hop le.</summary>
    Public Function TryMove(player As Integer, dx As Integer, dy As Integer) As Boolean
        If GameOver Then Return False
        If Not PlayerAlive(player) Then Return False

        Dim nx As Integer = PlayerX(player) + dx
        Dim ny As Integer = PlayerY(player) + dy

        If nx < 0 OrElse nx >= COLS OrElse ny < 0 OrElse ny >= ROWS Then Return False
        If Map(nx, ny) <> CellType.Empty Then Return False

        ' Kiem tra co bom o do khong
        Dim i As Integer
        For i = 0 To Bombs.Count - 1
            If Bombs(i).X = nx AndAlso Bombs(i).Y = ny Then Return False
        Next i

        ' Kiem tra co nguoi choi khac o do khong
        Dim other As Integer = 1 - player
        If PlayerAlive(other) AndAlso PlayerX(other) = nx AndAlso PlayerY(other) = ny Then Return False

        PlayerX(player) = nx
        PlayerY(player) = ny

        ' Kiem tra neu di vao lua
        CheckPlayerFireDamage(player)
        Return True
    End Function

    ''' <summary>Dat bom tai vi tri hien tai cua nguoi choi.</summary>
    Public Function TryPlaceBomb(player As Integer) As Boolean
        If GameOver Then Return False
        If Not PlayerAlive(player) Then Return False
        If PlayerBombCount(player) >= MAX_BOMBS Then Return False

        Dim bx As Integer = PlayerX(player)
        Dim by As Integer = PlayerY(player)

        ' Kiem tra da co bom chua
        Dim i As Integer
        For i = 0 To Bombs.Count - 1
            If Bombs(i).X = bx AndAlso Bombs(i).Y = by Then Return False
        Next i

        Dim b As New BombInfo()
        b.X = bx : b.Y = by : b.Owner = player
        b.Timer = BOMB_TIMER : b.Range = 2
        Bombs.Add(b)
        PlayerBombCount(player) += 1

        LastLog = String.Format("Player {0} dat bom tai ({1},{2})!", player + 1, bx, by)
        Return True
    End Function

    ''' <summary>Tick: giam timer bom va lua, kich no neu het timer. Goi moi 500ms tu host.</summary>
    Public Sub Tick()
        If GameOver Then Return
        TickCount += 1

        ' Giam timer lua
        Dim fi As Integer = 0
        Do While fi < Fires.Count
            Dim fc As FireCell = Fires(fi)
            fc.Timer -= 1
            If fc.Timer <= 0 Then
                Fires.RemoveAt(fi)
            Else
                Fires(fi) = fc
                fi += 1
            End If
        Loop

        ' Giam timer bom, no neu het
        Dim bi As Integer = 0
        Do While bi < Bombs.Count
            Dim bm As BombInfo = Bombs(bi)
            bm.Timer -= 1
            If bm.Timer <= 0 Then
                PlayerBombCount(bm.Owner) -= 1
                If PlayerBombCount(bm.Owner) < 0 Then PlayerBombCount(bm.Owner) = 0
                ExplodeBomb(bm)
                Bombs.RemoveAt(bi)
                ' Khong tang bi vi da xoa
            Else
                Bombs(bi) = bm
                bi += 1
            End If
        Loop

        CheckGameOver()
    End Sub

    Private Sub ExplodeBomb(bm As BombInfo)
        ' Them lua tai tam
        AddFire(bm.X, bm.Y)

        ' Lan lua 4 huong
        SpreadFire(bm.X, bm.Y, 1, 0, bm.Range)
        SpreadFire(bm.X, bm.Y, -1, 0, bm.Range)
        SpreadFire(bm.X, bm.Y, 0, 1, bm.Range)
        SpreadFire(bm.X, bm.Y, 0, -1, bm.Range)

        ' Kich no bom chain reaction
        Dim i As Integer = 0
        Do While i < Bombs.Count
            If Fires.Exists(Function(f) f.X = Bombs(i).X AndAlso f.Y = Bombs(i).Y) Then
                Dim chain As BombInfo = Bombs(i)
                PlayerBombCount(chain.Owner) -= 1
                If PlayerBombCount(chain.Owner) < 0 Then PlayerBombCount(chain.Owner) = 0
                ExplodeBomb(chain)
                Bombs.RemoveAt(i)
            Else
                i += 1
            End If
        Loop

        ' Kiem tra nguoi choi bi thuong
        CheckPlayerFireDamage(0)
        CheckPlayerFireDamage(1)

        LastLog = String.Format("BOM! Player {0} no tai ({1},{2}).", bm.Owner + 1, bm.X, bm.Y)
    End Sub

    Private Sub SpreadFire(ox As Integer, oy As Integer, dx As Integer, dy As Integer, range As Integer)
        Dim r As Integer
        For r = 1 To range
            Dim fx As Integer = ox + dx * r
            Dim fy As Integer = oy + dy * r
            If fx < 0 OrElse fx >= COLS OrElse fy < 0 OrElse fy >= ROWS Then Exit For
            If Map(fx, fy) = CellType.Wall Then Exit For
            AddFire(fx, fy)
            If Map(fx, fy) = CellType.Box Then
                Map(fx, fy) = CellType.Empty ' pha thung
                Exit For
            End If
        Next r
    End Sub

    Private Sub AddFire(fx As Integer, fy As Integer)
        ' Kiem tra da co lua chua
        Dim i As Integer
        For i = 0 To Fires.Count - 1
            If Fires(i).X = fx AndAlso Fires(i).Y = fy Then
                ' Reset timer
                Dim fc As FireCell = Fires(i)
                fc.Timer = FIRE_DURATION
                Fires(i) = fc
                Return
            End If
        Next i
        Dim nf As New FireCell()
        nf.X = fx : nf.Y = fy : nf.Timer = FIRE_DURATION
        Fires.Add(nf)
    End Sub

    Private Sub CheckPlayerFireDamage(player As Integer)
        If Not PlayerAlive(player) Then Return
        Dim px As Integer = PlayerX(player)
        Dim py As Integer = PlayerY(player)
        Dim i As Integer
        For i = 0 To Fires.Count - 1
            If Fires(i).X = px AndAlso Fires(i).Y = py Then
                PlayerAlive(player) = False
                LastLog = String.Format("Player {0} bi lua! Da chet.", player + 1)
                Return
            End If
        Next i
    End Sub

    Private Sub CheckGameOver()
        Dim p0 As Boolean = PlayerAlive(0)
        Dim p1 As Boolean = PlayerAlive(1)
        If Not p0 AndAlso Not p1 Then
            GameOver = True : Winner = -1
            LastLog = "HOA! Ca 2 bi no cung luc!"
        ElseIf Not p0 Then
            GameOver = True : Winner = 1
            LastLog = "Player 2 (Khach) THANG!"
        ElseIf Not p1 Then
            GameOver = True : Winner = 0
            LastLog = "Player 1 (Host) THANG!"
        End If
    End Sub

    Public Function HasFire(x As Integer, y As Integer) As Boolean
        Dim i As Integer
        For i = 0 To Fires.Count - 1
            If Fires(i).X = x AndAlso Fires(i).Y = y Then Return True
        Next i
        Return False
    End Function

    Public Function HasBomb(x As Integer, y As Integer) As Boolean
        Dim i As Integer
        For i = 0 To Bombs.Count - 1
            If Bombs(i).X = x AndAlso Bombs(i).Y = y Then Return True
        Next i
        Return False
    End Function

    Public Function GetBombTimer(x As Integer, y As Integer) As Integer
        Dim i As Integer
        For i = 0 To Bombs.Count - 1
            If Bombs(i).X = x AndAlso Bombs(i).Y = y Then Return Bombs(i).Timer
        Next i
        Return -1
    End Function

    ' ============================================================
    '  SERIALIZE / DESERIALIZE cho mang
    ' ============================================================
    Public Function Serialize() As String
        Dim sb As New StringBuilder()

        ' Map (chi gui khi co thay doi, nhung o day gui full cho don gian)
        Dim x As Integer, y As Integer
        For y = 0 To ROWS - 1
            For x = 0 To COLS - 1
                sb.Append(CInt(Map(x, y)).ToString())
                If Not (x = COLS - 1 AndAlso y = ROWS - 1) Then sb.Append(",")
            Next x
        Next y
        sb.Append("|")

        ' Players
        sb.Append(PlayerX(0).ToString()) : sb.Append(",")
        sb.Append(PlayerY(0).ToString()) : sb.Append(",")
        sb.Append(If(PlayerAlive(0), "1", "0")) : sb.Append(",")
        sb.Append(PlayerBombCount(0).ToString()) : sb.Append("|")

        sb.Append(PlayerX(1).ToString()) : sb.Append(",")
        sb.Append(PlayerY(1).ToString()) : sb.Append(",")
        sb.Append(If(PlayerAlive(1), "1", "0")) : sb.Append(",")
        sb.Append(PlayerBombCount(1).ToString()) : sb.Append("|")

        ' Bombs
        Dim i As Integer
        For i = 0 To Bombs.Count - 1
            Dim b As BombInfo = Bombs(i)
            sb.Append(b.X.ToString()) : sb.Append(",")
            sb.Append(b.Y.ToString()) : sb.Append(",")
            sb.Append(b.Owner.ToString()) : sb.Append(",")
            sb.Append(b.Timer.ToString()) : sb.Append(",")
            sb.Append(b.Range.ToString())
            If i < Bombs.Count - 1 Then sb.Append(";")
        Next i
        sb.Append("|")

        ' Fires
        For i = 0 To Fires.Count - 1
            Dim f As FireCell = Fires(i)
            sb.Append(f.X.ToString()) : sb.Append(",")
            sb.Append(f.Y.ToString()) : sb.Append(",")
            sb.Append(f.Timer.ToString())
            If i < Fires.Count - 1 Then sb.Append(";")
        Next i
        sb.Append("|")

        ' GameOver, Winner, LastLog
        sb.Append(If(GameOver, "1", "0")) : sb.Append("|")
        sb.Append(Winner.ToString()) : sb.Append("|")
        sb.Append(LastLog.Replace("|", " ").Replace(Chr(13), " ").Replace(Chr(10), " "))

        Return sb.ToString()
    End Function

    Public Sub Deserialize(data As String)
        Dim parts As String() = data.Split("|"c)
        If parts.Length < 7 Then Return

        ' Map
        Dim mapParts As String() = parts(0).Split(","c)
        Dim idx As Integer = 0
        Dim x As Integer, y As Integer
        For y = 0 To ROWS - 1
            For x = 0 To COLS - 1
                If idx < mapParts.Length Then
                    Dim v As Integer = 0
                    Integer.TryParse(mapParts(idx), v)
                    Map(x, y) = CType(v, CellType)
                End If
                idx += 1
            Next x
        Next y

        ' Players
        Dim p0 As String() = parts(1).Split(","c)
        If p0.Length >= 4 Then
            Integer.TryParse(p0(0), PlayerX(0))
            Integer.TryParse(p0(1), PlayerY(0))
            PlayerAlive(0) = (p0(2) = "1")
            Integer.TryParse(p0(3), PlayerBombCount(0))
        End If
        Dim p1 As String() = parts(2).Split(","c)
        If p1.Length >= 4 Then
            Integer.TryParse(p1(0), PlayerX(1))
            Integer.TryParse(p1(1), PlayerY(1))
            PlayerAlive(1) = (p1(2) = "1")
            Integer.TryParse(p1(3), PlayerBombCount(1))
        End If

        ' Bombs
        Bombs.Clear()
        If parts(3).Length > 0 Then
            Dim bombEntries As String() = parts(3).Split(";"c)
            Dim i As Integer
            For i = 0 To bombEntries.Length - 1
                If bombEntries(i).Length = 0 Then Continue For
                Dim bp As String() = bombEntries(i).Split(","c)
                If bp.Length >= 5 Then
                    Dim b As New BombInfo()
                    Integer.TryParse(bp(0), b.X)
                    Integer.TryParse(bp(1), b.Y)
                    Integer.TryParse(bp(2), b.Owner)
                    Integer.TryParse(bp(3), b.Timer)
                    Integer.TryParse(bp(4), b.Range)
                    Bombs.Add(b)
                End If
            Next i
        End If

        ' Fires
        Fires.Clear()
        If parts(4).Length > 0 Then
            Dim fireEntries As String() = parts(4).Split(";"c)
            Dim i As Integer
            For i = 0 To fireEntries.Length - 1
                If fireEntries(i).Length = 0 Then Continue For
                Dim fp As String() = fireEntries(i).Split(","c)
                If fp.Length >= 3 Then
                    Dim f As New FireCell()
                    Integer.TryParse(fp(0), f.X)
                    Integer.TryParse(fp(1), f.Y)
                    Integer.TryParse(fp(2), f.Timer)
                    Fires.Add(f)
                End If
            Next i
        End If

        ' GameOver, Winner, LastLog
        GameOver = (parts(5) = "1")
        Integer.TryParse(parts(6), Winner)
        If parts.Length >= 8 Then LastLog = parts(7)
    End Sub

End Class
