Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic
Imports System.Drawing

Public Class BombGame

    Public Const COLS As Integer = 13
    Public Const ROWS As Integer = 11
    Public Const BOMB_TIMER As Integer = 5
    Public Const FIRE_DURATION As Integer = 3

    Public Enum CellType As Byte
        Empty = 0
        Wall = 1
        Box = 2
    End Enum

    Public Enum PowerupType As Byte
        Range = 0
        BombUp = 1
        Speed = 2
    End Enum

    Public Structure PowerupInfo
        Public X As Integer
        Public Y As Integer
        Public Kind As PowerupType
    End Structure

    Public Structure BombInfo
        Public X As Integer
        Public Y As Integer
        Public Owner As Integer
        Public Timer As Integer
        Public Range As Integer
    End Structure

    Public Structure FireCell
        Public X As Integer
        Public Y As Integer
        Public Timer As Integer
    End Structure

    Public Structure MonsterInfo
        Public X As Integer
        Public Y As Integer
        Public Alive As Boolean
        Public MoveTimer As Integer  ' dem tick de di chuyen
    End Structure

    ' --- Ban do ---
    Public Map(COLS - 1, ROWS - 1) As CellType
    Private rng As New Random()

    ' --- Nguoi choi ---
    Public PlayerX(1) As Integer
    Public PlayerY(1) As Integer
    Public PlayerAlive(1) As Boolean
    Public PlayerBombCount(1) As Integer
    Public PlayerRange(1) As Integer
    Public PlayerMaxBombs(1) As Integer
    Public PlayerSpeed(1) As Integer

    ' --- Danh sach doi tuong ---
    Public Bombs As New List(Of BombInfo)()
    Public Fires As New List(Of FireCell)()
    Public Powerups As New List(Of PowerupInfo)()
    Public Monsters As New List(Of MonsterInfo)()

    ' --- Trang thai game ---
    Public GameOver As Boolean
    Public Winner As Integer   ' -1=hoa/thua, 0=P1 thang, 99=tieu diet het monster
    Public LastLog As String
    Public TickCount As Integer

    ' --- Mode ---
    Public IsPvAI As Boolean   ' True = choi voi monster, False = PvP

    Public Sub New()
        ResetBoard()
    End Sub

    Public Sub ResetBoard()
        Dim x As Integer, y As Integer

        For y = 0 To ROWS - 1
            For x = 0 To COLS - 1
                If x = 0 OrElse x = COLS - 1 OrElse y = 0 OrElse y = ROWS - 1 Then
                    Map(x, y) = CellType.Wall
                ElseIf x Mod 2 = 0 AndAlso y Mod 2 = 0 Then
                    Map(x, y) = CellType.Wall
                Else
                    Map(x, y) = CellType.Empty
                End If
            Next x
        Next y

        Dim mapRng As New Random()
        For y = 1 To ROWS - 2
            For x = 1 To COLS - 2
                If Map(x, y) = CellType.Empty Then
                    Dim safeP1 As Boolean = (x <= 2 AndAlso y <= 2)
                    Dim safeP2 As Boolean = (x >= COLS - 3 AndAlso y >= ROWS - 3)
                    If Not safeP1 AndAlso Not safeP2 Then
                        If mapRng.Next(100) < 65 Then
                            Map(x, y) = CellType.Box
                        End If
                    End If
                End If
            Next x
        Next y

        PlayerX(0) = 1 : PlayerY(0) = 1
        PlayerX(1) = COLS - 2 : PlayerY(1) = ROWS - 2
        PlayerAlive(0) = True
        PlayerAlive(1) = True
        PlayerBombCount(0) = 0
        PlayerBombCount(1) = 0
        PlayerRange(0) = 2 : PlayerRange(1) = 2
        PlayerMaxBombs(0) = 1 : PlayerMaxBombs(1) = 1
        PlayerSpeed(0) = 0 : PlayerSpeed(1) = 0

        Bombs.Clear()
        Fires.Clear()
        Powerups.Clear()
        Monsters.Clear()
        GameOver = False
        Winner = -1
        LastLog = "Bat dau! WASD/Mui ten di chuyen, Space dat bom."
        TickCount = 0
    End Sub

    ' Goi sau ResetBoard khi choi PvAI
    Public Sub SpawnMonsters(count As Integer)
        Monsters.Clear()
        Dim candidates As New List(Of Point)()
        Dim x As Integer, y As Integer
        For y = 1 To ROWS - 2
            For x = 1 To COLS - 2
                If Map(x, y) = CellType.Empty Then
                    ' Tranh vung an toan cua player (3x3 goc trai tren va phai duoi)
                    Dim safeP1 As Boolean = (x <= 2 AndAlso y <= 2)
                    Dim safeP2 As Boolean = (x >= COLS - 3 AndAlso y >= ROWS - 3)
                    If Not safeP1 AndAlso Not safeP2 Then
                        candidates.Add(New Point(x, y))
                    End If
                End If
            Next x
        Next y
        ' Xao tron
        Dim i As Integer
        For i = candidates.Count - 1 To 1 Step -1
            Dim j As Integer = rng.Next(i + 1)
            Dim tmp As Point = candidates(i)
            candidates(i) = candidates(j)
            candidates(j) = tmp
        Next i
        Dim n As Integer = Math.Min(count, candidates.Count)
        For i = 0 To n - 1
            Dim m As New MonsterInfo()
            m.X = candidates(i).X
            m.Y = candidates(i).Y
            m.Alive = True
            m.MoveTimer = 2
            Monsters.Add(m)
        Next i
    End Sub

    Public Function TryMove(player As Integer, dx As Integer, dy As Integer) As Boolean
        If GameOver Then Return False
        If Not PlayerAlive(player) Then Return False

        Dim nx As Integer = PlayerX(player) + dx
        Dim ny As Integer = PlayerY(player) + dy

        If nx < 0 OrElse nx >= COLS OrElse ny < 0 OrElse ny >= ROWS Then Return False
        If Map(nx, ny) <> CellType.Empty Then Return False

        Dim i As Integer
        For i = 0 To Bombs.Count - 1
            If Bombs(i).X = nx AndAlso Bombs(i).Y = ny Then Return False
        Next i

        If Not IsPvAI Then
            Dim other As Integer = 1 - player
            If PlayerAlive(other) AndAlso PlayerX(other) = nx AndAlso PlayerY(other) = ny Then Return False
        End If

        PlayerX(player) = nx
        PlayerY(player) = ny

        PickupPowerup(player)

        If PlayerSpeed(player) >= 1 Then
            Dim nx2 As Integer = PlayerX(player) + dx
            Dim ny2 As Integer = PlayerY(player) + dy
            If nx2 >= 0 AndAlso nx2 < COLS AndAlso ny2 >= 0 AndAlso ny2 < ROWS Then
                If Map(nx2, ny2) = CellType.Empty Then
                    Dim blocked As Boolean = False
                    Dim j As Integer
                    For j = 0 To Bombs.Count - 1
                        If Bombs(j).X = nx2 AndAlso Bombs(j).Y = ny2 Then blocked = True : Exit For
                    Next j
                    If Not blocked Then
                        PlayerX(player) = nx2
                        PlayerY(player) = ny2
                        PickupPowerup(player)
                    End If
                End If
            End If
        End If

        CheckPlayerFireDamage(player)
        CheckMonsterContact(player)
        Return True
    End Function

    Public Function TryPlaceBomb(player As Integer) As Boolean
        If GameOver Then Return False
        If Not PlayerAlive(player) Then Return False
        If PlayerBombCount(player) >= PlayerMaxBombs(player) Then Return False

        Dim bx As Integer = PlayerX(player)
        Dim by As Integer = PlayerY(player)

        Dim i As Integer
        For i = 0 To Bombs.Count - 1
            If Bombs(i).X = bx AndAlso Bombs(i).Y = by Then Return False
        Next i

        Dim b As New BombInfo()
        b.X = bx : b.Y = by : b.Owner = player
        b.Timer = BOMB_TIMER : b.Range = PlayerRange(player)
        Bombs.Add(b)
        PlayerBombCount(player) += 1

        LastLog = String.Format("Player {0} dat bom tai ({1},{2})!", player + 1, bx, by)
        Return True
    End Function

    Public Sub Tick()
        If GameOver Then Return
        TickCount += 1

        ' === Buoc 1: No bom het gio TRUOC (de lua moi sinh ra trong tick nay) ===
        Dim explodeList As New List(Of BombInfo)()
        Dim bi As Integer
        For bi = 0 To Bombs.Count - 1
            Dim bm As BombInfo = Bombs(bi)
            bm.Timer -= 1
            If bm.Timer <= 0 Then
                explodeList.Add(bm)
            Else
                Bombs(bi) = bm
            End If
        Next bi
        For bi = 0 To explodeList.Count - 1
            Dim bm As BombInfo = explodeList(bi)
            Dim foundAt As Integer = -1
            Dim si As Integer
            For si = 0 To Bombs.Count - 1
                If Bombs(si).X = bm.X AndAlso Bombs(si).Y = bm.Y AndAlso Bombs(si).Owner = bm.Owner Then
                    foundAt = si : Exit For
                End If
            Next si
            If foundAt >= 0 Then
                PlayerBombCount(bm.Owner) -= 1
                If PlayerBombCount(bm.Owner) < 0 Then PlayerBombCount(bm.Owner) = 0
                Bombs.RemoveAt(foundAt)
                ExplodeBomb(bm)
            End If
        Next bi

        ' === Buoc 2: Giam timer lua (lua cu het han moi xoa, lua moi tu bom van con) ===
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

        ' === Buoc 3: Di chuyen monster, check cham lua/player ===
        If IsPvAI Then
            MoveMonsters()
        End If

        CheckGameOver()
    End Sub

    Private Sub MoveMonsters()
        Dim dirs() As Point = {New Point(1, 0), New Point(-1, 0), New Point(0, 1), New Point(0, -1)}
        Dim i As Integer
        For i = 0 To Monsters.Count - 1
            If Not Monsters(i).Alive Then Continue For
            Dim m As MonsterInfo = Monsters(i)
            m.MoveTimer -= 1
            If m.MoveTimer > 0 Then
                Monsters(i) = m
                Continue For
            End If
            m.MoveTimer = 2  ' reset

            ' Xao tron huong di
            Dim order() As Integer = {0, 1, 2, 3}
            Dim j As Integer
            For j = 3 To 1 Step -1
                Dim k As Integer = rng.Next(j + 1)
                Dim tmp As Integer = order(j)
                order(j) = order(k)
                order(k) = tmp
            Next j

            ' Thu di 1 huong hop le
            For j = 0 To 3
                Dim d As Point = dirs(order(j))
                Dim nx As Integer = m.X + d.X
                Dim ny As Integer = m.Y + d.Y
                If nx < 0 OrElse nx >= COLS OrElse ny < 0 OrElse ny >= ROWS Then Continue For
                If Map(nx, ny) <> CellType.Empty Then Continue For

                ' Tranh bom
                Dim hasBomb As Boolean = False
                Dim bi As Integer
                For bi = 0 To Bombs.Count - 1
                    If Bombs(bi).X = nx AndAlso Bombs(bi).Y = ny Then hasBomb = True : Exit For
                Next bi
                If hasBomb Then Continue For

                ' Tranh monster khac
                Dim hasMonster As Boolean = False
                Dim mi As Integer
                For mi = 0 To Monsters.Count - 1
                    If mi = i Then Continue For
                    If Monsters(mi).Alive AndAlso Monsters(mi).X = nx AndAlso Monsters(mi).Y = ny Then
                        hasMonster = True : Exit For
                    End If
                Next mi
                If hasMonster Then Continue For

                m.X = nx : m.Y = ny
                Exit For
            Next j

            Monsters(i) = m

            ' Kiem tra cham lua
            Dim fi As Integer
            For fi = 0 To Fires.Count - 1
                If Fires(fi).X = m.X AndAlso Fires(fi).Y = m.Y Then
                    Dim dead As MonsterInfo = Monsters(i)
                    dead.Alive = False
                    Monsters(i) = dead
                    LastLog = "Monster bi tieu diet!"
                    Exit For
                End If
            Next fi

            ' Kiem tra cham player
            If Monsters(i).Alive Then
                Dim p As Integer
                For p = 0 To 0  ' chi player 0 trong PvAI
                    If PlayerAlive(p) AndAlso PlayerX(p) = Monsters(i).X AndAlso PlayerY(p) = Monsters(i).Y Then
                        PlayerAlive(p) = False
                        LastLog = "Player bi monster an mat!"
                    End If
                Next p
            End If
        Next i
    End Sub

    Private Sub ExplodeBomb(bm As BombInfo)
        AddFire(bm.X, bm.Y)
        SpreadFire(bm.X, bm.Y, 1, 0, bm.Range)
        SpreadFire(bm.X, bm.Y, -1, 0, bm.Range)
        SpreadFire(bm.X, bm.Y, 0, 1, bm.Range)
        SpreadFire(bm.X, bm.Y, 0, -1, bm.Range)

        ' Chain reaction
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

        CheckPlayerFireDamage(0)
        If Not IsPvAI Then CheckPlayerFireDamage(1)

        ' Kiem tra monster bi lua
        If IsPvAI Then
            For i = 0 To Monsters.Count - 1
                If Not Monsters(i).Alive Then Continue For
                Dim mi As MonsterInfo = Monsters(i)
                Dim fi As Integer
                For fi = 0 To Fires.Count - 1
                    If Fires(fi).X = mi.X AndAlso Fires(fi).Y = mi.Y Then
                        mi.Alive = False
                        Monsters(i) = mi
                        LastLog = "Monster bi tieu diet!"
                        Exit For
                    End If
                Next fi
            Next i
        End If

        LastLog = String.Format("BOM no tai ({0},{1})!", bm.X, bm.Y)
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
                Map(fx, fy) = CellType.Empty
                TrySpawnPowerup(fx, fy)
                ' Khong BurnPowerup o day - powerup vua duoc spawn tu hop nay
                Exit For
            End If
            BurnPowerupAt(fx, fy)
        Next r
    End Sub

    Private Sub TrySpawnPowerup(fx As Integer, fy As Integer)
        If rng.Next(100) >= 40 Then Return
        Dim pw As New PowerupInfo()
        pw.X = fx : pw.Y = fy : pw.Kind = CType(rng.Next(3), PowerupType)
        Powerups.Add(pw)
    End Sub

    Private Sub BurnPowerupAt(fx As Integer, fy As Integer)
        Dim i As Integer = 0
        Do While i < Powerups.Count
            If Powerups(i).X = fx AndAlso Powerups(i).Y = fy Then
                Powerups.RemoveAt(i)
            Else
                i += 1
            End If
        Loop
    End Sub

    Private Sub PickupPowerup(player As Integer)
        Dim px As Integer = PlayerX(player)
        Dim py As Integer = PlayerY(player)
        Dim i As Integer = 0
        Do While i < Powerups.Count
            If Powerups(i).X = px AndAlso Powerups(i).Y = py Then
                Select Case Powerups(i).Kind
                    Case PowerupType.Range
                        PlayerRange(player) += 1
                        LastLog = String.Format("Nhat RANGE! Tam no: {0}", PlayerRange(player))
                    Case PowerupType.BombUp
                        PlayerMaxBombs(player) += 1
                        LastLog = String.Format("Nhat BOMB UP! So bom: {0}", PlayerMaxBombs(player))
                    Case PowerupType.Speed
                        If PlayerSpeed(player) < 1 Then PlayerSpeed(player) = 1
                        LastLog = "Nhat SPEED! Di chuyen nhanh hon!"
                End Select
                Powerups.RemoveAt(i)
                Return
            End If
            i += 1
        Loop
    End Sub

    Private Sub AddFire(fx As Integer, fy As Integer)
        Dim i As Integer
        For i = 0 To Fires.Count - 1
            If Fires(i).X = fx AndAlso Fires(i).Y = fy Then
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
        Dim i As Integer
        For i = 0 To Fires.Count - 1
            If Fires(i).X = PlayerX(player) AndAlso Fires(i).Y = PlayerY(player) Then
                PlayerAlive(player) = False
                LastLog = String.Format("Player {0} bi lua! Da chet.", player + 1)
                Return
            End If
        Next i
    End Sub

    Private Sub CheckMonsterContact(player As Integer)
        If Not PlayerAlive(player) Then Return
        Dim i As Integer
        For i = 0 To Monsters.Count - 1
            If Monsters(i).Alive AndAlso Monsters(i).X = PlayerX(player) AndAlso Monsters(i).Y = PlayerY(player) Then
                PlayerAlive(player) = False
                LastLog = "Player bi monster an mat!"
                Return
            End If
        Next i
    End Sub

    Private Sub CheckGameOver()
        If IsPvAI Then
            ' Player chet -> thua
            If Not PlayerAlive(0) Then
                GameOver = True : Winner = -1
                LastLog = "Ban da thua! Monster chien thang!"
                Return
            End If
            ' Het monster -> thang
            Dim anyAlive As Boolean = False
            Dim i As Integer
            For i = 0 To Monsters.Count - 1
                If Monsters(i).Alive Then anyAlive = True : Exit For
            Next i
            If Not anyAlive AndAlso Monsters.Count > 0 Then
                GameOver = True : Winner = 99
                LastLog = "CHIEN THANG! Ban da tieu diet het monster!"
            End If
        Else
            ' PvP cu
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

    Public Function CountAliveMonsters() As Integer
        Dim cnt As Integer = 0
        Dim i As Integer
        For i = 0 To Monsters.Count - 1
            If Monsters(i).Alive Then cnt += 1
        Next i
        Return cnt
    End Function

    ' ============================================================
    '  SERIALIZE / DESERIALIZE cho PvP mang
    ' ============================================================
    Public Function Serialize() As String
        Dim sb As New StringBuilder()

        Dim x As Integer, y As Integer
        For y = 0 To ROWS - 1
            For x = 0 To COLS - 1
                sb.Append(CInt(Map(x, y)).ToString())
                If Not (x = COLS - 1 AndAlso y = ROWS - 1) Then sb.Append(",")
            Next x
        Next y
        sb.Append("|")

        Dim i As Integer
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

        ' Powerups
        For i = 0 To Powerups.Count - 1
            Dim pw As PowerupInfo = Powerups(i)
            sb.Append(pw.X.ToString()) : sb.Append(",")
            sb.Append(pw.Y.ToString()) : sb.Append(",")
            sb.Append(CInt(pw.Kind).ToString())
            If i < Powerups.Count - 1 Then sb.Append(";")
        Next i
        sb.Append("|")

        ' Player stats
        sb.Append(PlayerRange(0).ToString()) : sb.Append(",")
        sb.Append(PlayerMaxBombs(0).ToString()) : sb.Append(",")
        sb.Append(PlayerSpeed(0).ToString()) : sb.Append("|")
        sb.Append(PlayerRange(1).ToString()) : sb.Append(",")
        sb.Append(PlayerMaxBombs(1).ToString()) : sb.Append(",")
        sb.Append(PlayerSpeed(1).ToString()) : sb.Append("|")

        ' GameOver, Winner, LastLog
        sb.Append(If(GameOver, "1", "0")) : sb.Append("|")
        sb.Append(Winner.ToString()) : sb.Append("|")
        sb.Append(LastLog.Replace("|", " ").Replace(Chr(13), " ").Replace(Chr(10), " "))

        Return sb.ToString()
    End Function

    Public Sub Deserialize(data As String)
        Dim parts As String() = data.Split("|"c)
        If parts.Length < 10 Then Return

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

        Dim p0 As String() = parts(1).Split(","c)
        If p0.Length >= 4 Then
            Integer.TryParse(p0(0), PlayerX(0)) : Integer.TryParse(p0(1), PlayerY(0))
            PlayerAlive(0) = (p0(2) = "1") : Integer.TryParse(p0(3), PlayerBombCount(0))
        End If
        Dim p1 As String() = parts(2).Split(","c)
        If p1.Length >= 4 Then
            Integer.TryParse(p1(0), PlayerX(1)) : Integer.TryParse(p1(1), PlayerY(1))
            PlayerAlive(1) = (p1(2) = "1") : Integer.TryParse(p1(3), PlayerBombCount(1))
        End If

        Bombs.Clear()
        If parts(3).Length > 0 Then
            Dim i As Integer
            For Each entry As String In parts(3).Split(";"c)
                If entry.Length = 0 Then Continue For
                Dim bp As String() = entry.Split(","c)
                If bp.Length >= 5 Then
                    Dim b As New BombInfo()
                    Integer.TryParse(bp(0), b.X) : Integer.TryParse(bp(1), b.Y)
                    Integer.TryParse(bp(2), b.Owner) : Integer.TryParse(bp(3), b.Timer)
                    Integer.TryParse(bp(4), b.Range)
                    Bombs.Add(b)
                End If
            Next
        End If

        Fires.Clear()
        If parts(4).Length > 0 Then
            For Each entry As String In parts(4).Split(";"c)
                If entry.Length = 0 Then Continue For
                Dim fp As String() = entry.Split(","c)
                If fp.Length >= 3 Then
                    Dim f As New FireCell()
                    Integer.TryParse(fp(0), f.X) : Integer.TryParse(fp(1), f.Y)
                    Integer.TryParse(fp(2), f.Timer)
                    Fires.Add(f)
                End If
            Next
        End If

        Powerups.Clear()
        If parts(5).Length > 0 Then
            For Each entry As String In parts(5).Split(";"c)
                If entry.Length = 0 Then Continue For
                Dim pp As String() = entry.Split(","c)
                If pp.Length >= 3 Then
                    Dim pw As New PowerupInfo()
                    Integer.TryParse(pp(0), pw.X) : Integer.TryParse(pp(1), pw.Y)
                    Dim kv As Integer = 0 : Integer.TryParse(pp(2), kv)
                    pw.Kind = CType(kv, PowerupType)
                    Powerups.Add(pw)
                End If
            Next
        End If

        Dim s0 As String() = parts(6).Split(","c)
        If s0.Length >= 3 Then
            Integer.TryParse(s0(0), PlayerRange(0)) : Integer.TryParse(s0(1), PlayerMaxBombs(0))
            Integer.TryParse(s0(2), PlayerSpeed(0))
        End If
        Dim s1 As String() = parts(7).Split(","c)
        If s1.Length >= 3 Then
            Integer.TryParse(s1(0), PlayerRange(1)) : Integer.TryParse(s1(1), PlayerMaxBombs(1))
            Integer.TryParse(s1(2), PlayerSpeed(1))
        End If

        GameOver = (parts(8) = "1")
        Integer.TryParse(parts(9), Winner)
        If parts.Length >= 11 Then LastLog = parts(10)
    End Sub

End Class
