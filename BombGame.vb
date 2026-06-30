Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic
Imports System.Drawing

Public Class BombGame

    Public Const COLS As Integer = 13
    Public Const ROWS As Integer = 11
    Public Const BOMB_TIMER As Integer = 5
    Public Const FIRE_DURATION As Integer = 2

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

        ' === Buoc 1: No bom het gio ===
        ' Giam timer tat ca bom, thu thap index can no
        Dim explodeList As New List(Of BombInfo)()
        Dim removeIndices As New List(Of Integer)()
        Dim bi As Integer
        For bi = 0 To Bombs.Count - 1
            Dim bm As BombInfo = Bombs(bi)
            bm.Timer -= 1
            Bombs(bi) = bm
            If bm.Timer <= 0 Then
                explodeList.Add(bm)
                removeIndices.Add(bi)
            End If
        Next bi

        ' Xoa bom tu cuoi xuong dau de index khong bi lech
        removeIndices.Sort()
        Dim ri As Integer
        For ri = removeIndices.Count - 1 To 0 Step -1
            Dim idx As Integer = removeIndices(ri)
            Dim owner As Integer = Bombs(idx).Owner
            PlayerBombCount(owner) -= 1
            If PlayerBombCount(owner) < 0 Then PlayerBombCount(owner) = 0
            Bombs.RemoveAt(idx)
        Next ri

        ' Kich no tung qua - dung Set vi tri da no de tranh no 2 lan
        ' (co the xay ra neu bom A chain kich bom B, roi B lai co trong explodeList)
        Dim explodedPos As New HashSet(Of Long)()
        For bi = 0 To explodeList.Count - 1
            Dim bm As BombInfo = explodeList(bi)
            Dim posKey As Long = CLng(bm.X) * 100L + CLng(bm.Y)
            If Not explodedPos.Contains(posKey) Then
                explodedPos.Add(posKey)
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

    ' BFS tim buoc di dau tien ngan nhat tu (sx,sy) den (tx,ty)
    ' Tra ve Point buoc di tiep theo, hoac Point(-1,-1) neu khong tim duoc duong
    Private Function BfsNextStep(sx As Integer, sy As Integer, tx As Integer, ty As Integer, monsterIdx As Integer) As Point
        If sx = tx AndAlso sy = ty Then Return New Point(-1, -1)

        Dim dirs() As Point = {New Point(1, 0), New Point(-1, 0), New Point(0, 1), New Point(0, -1)}
        Dim visited(COLS - 1, ROWS - 1) As Boolean
        Dim fromDir(COLS - 1, ROWS - 1) As Integer  ' huong di de den o nay tu goc
        Dim queue As New Queue(Of Point)()

        visited(sx, sy) = True
        queue.Enqueue(New Point(sx, sy))
        Dim found As Boolean = False

        Dim di As Integer
        ' Khoi tao fromDir = -1
        Dim fy As Integer, fx As Integer
        For fy = 0 To ROWS - 1
            For fx = 0 To COLS - 1
                fromDir(fx, fy) = -1
            Next fx
        Next fy

        Do While queue.Count > 0 AndAlso Not found
            Dim cur As Point = queue.Dequeue()
            For di = 0 To 3
                Dim nx As Integer = cur.X + dirs(di).X
                Dim ny As Integer = cur.Y + dirs(di).Y
                If nx < 0 OrElse nx >= COLS OrElse ny < 0 OrElse ny >= ROWS Then Continue For
                If visited(nx, ny) Then Continue For
                If Map(nx, ny) <> CellType.Empty Then Continue For

                ' Tranh bom va lua (nguy hiem)
                Dim danger As Boolean = False
                Dim bi As Integer
                For bi = 0 To Bombs.Count - 1
                    If Bombs(bi).X = nx AndAlso Bombs(bi).Y = ny Then danger = True : Exit For
                Next bi
                If danger Then Continue For
                Dim fi As Integer
                For fi = 0 To Fires.Count - 1
                    If Fires(fi).X = nx AndAlso Fires(fi).Y = ny Then danger = True : Exit For
                Next fi
                If danger Then Continue For

                ' Tranh monster khac (tru ban than)
                Dim hasM As Boolean = False
                Dim mi As Integer
                For mi = 0 To Monsters.Count - 1
                    If mi = monsterIdx Then Continue For
                    If Monsters(mi).Alive AndAlso Monsters(mi).X = nx AndAlso Monsters(mi).Y = ny Then
                        hasM = True : Exit For
                    End If
                Next mi
                If hasM Then Continue For

                visited(nx, ny) = True
                ' Luu huong di tu goc (sx,sy)
                If cur.X = sx AndAlso cur.Y = sy Then
                    fromDir(nx, ny) = di
                Else
                    fromDir(nx, ny) = fromDir(cur.X, cur.Y)
                End If

                If nx = tx AndAlso ny = ty Then
                    found = True : Exit For
                End If
                queue.Enqueue(New Point(nx, ny))
            Next di
        Loop

        If Not found Then Return New Point(-1, -1)
        Dim stepDir As Integer = fromDir(tx, ty)
        If stepDir < 0 Then Return New Point(-1, -1)
        Return dirs(stepDir)
    End Function

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

            ' Chon buoc di: 70% BFS duoi player, 30% random de game khong qua kho
            Dim chosenDX As Integer = 0, chosenDY As Integer = 0
            Dim moved As Boolean = False

            If PlayerAlive(0) AndAlso rng.Next(100) < 70 Then
                ' BFS den player
                Dim nextStep As Point = BfsNextStep(m.X, m.Y, PlayerX(0), PlayerY(0), i)
                If nextStep.X <> -1 Then
                    chosenDX = nextStep.X : chosenDY = nextStep.Y
                    moved = True
                End If
            End If

            If Not moved Then
                ' Fallback: random (xao tron huong)
                Dim order() As Integer = {0, 1, 2, 3}
                Dim j As Integer
                For j = 3 To 1 Step -1
                    Dim k As Integer = rng.Next(j + 1)
                    Dim tmp As Integer = order(j)
                    order(j) = order(k)
                    order(k) = tmp
                Next j
                For j = 0 To 3
                    Dim d As Point = dirs(order(j))
                    Dim nx As Integer = m.X + d.X
                    Dim ny As Integer = m.Y + d.Y
                    If nx < 0 OrElse nx >= COLS OrElse ny < 0 OrElse ny >= ROWS Then Continue For
                    If Map(nx, ny) <> CellType.Empty Then Continue For
                    Dim hasBomb As Boolean = False
                    Dim bi As Integer
                    For bi = 0 To Bombs.Count - 1
                        If Bombs(bi).X = nx AndAlso Bombs(bi).Y = ny Then hasBomb = True : Exit For
                    Next bi
                    If hasBomb Then Continue For
                    Dim hasMonster As Boolean = False
                    Dim mi As Integer
                    For mi = 0 To Monsters.Count - 1
                        If mi = i Then Continue For
                        If Monsters(mi).Alive AndAlso Monsters(mi).X = nx AndAlso Monsters(mi).Y = ny Then
                            hasMonster = True : Exit For
                        End If
                    Next mi
                    If hasMonster Then Continue For
                    chosenDX = d.X : chosenDY = d.Y
                    moved = True
                    Exit For
                Next j
            End If

            If moved Then
                m.X += chosenDX : m.Y += chosenDY
            End If

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
        ' Dung queue de xu ly chain reaction (bom trong tam no cua bom khac cung no theo)
        ' Luu y: bm da duoc xoa khoi Bombs truoc khi goi ham nay (boi Tick)
        ' Chi bom trong Bombs (chua het gio) moi co the bi kich no day chuyen o day
        Dim queue As New Queue(Of BombInfo)()
        queue.Enqueue(bm)

        Do While queue.Count > 0
            Dim cur As BombInfo = queue.Dequeue()

            AddFire(cur.X, cur.Y)
            SpreadFire(cur.X, cur.Y, 1, 0, cur.Range)
            SpreadFire(cur.X, cur.Y, -1, 0, cur.Range)
            SpreadFire(cur.X, cur.Y, 0, 1, cur.Range)
            SpreadFire(cur.X, cur.Y, 0, -1, cur.Range)

            ' Tim bom con lai trong Bombs bi lua cham -> kich no day chuyen
            Dim i As Integer = 0
            Do While i < Bombs.Count
                Dim candidate As BombInfo = Bombs(i)
                If Fires.Exists(Function(f) f.X = candidate.X AndAlso f.Y = candidate.Y) Then
                    ' Xoa khoi Bombs truoc, giam bom count, roi enqueue
                    PlayerBombCount(candidate.Owner) -= 1
                    If PlayerBombCount(candidate.Owner) < 0 Then PlayerBombCount(candidate.Owner) = 0
                    Bombs.RemoveAt(i)
                    queue.Enqueue(candidate)
                    ' Khong tang i vi da xoa phan tu tai i
                Else
                    i += 1
                End If
            Loop
        Loop

        ' Sau khi tat ca bom no xong, check dame
        CheckPlayerFireDamage(0)
        If Not IsPvAI Then CheckPlayerFireDamage(1)

        ' Kiem tra monster bi lua
        If IsPvAI Then
            Dim i As Integer
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
