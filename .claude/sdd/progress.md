# SDD Progress Ledger

- Task 0: complete (dotnet sln add tests\脚本.Tests\脚本.Tests.csproj; baseline 15/15 pass)
- Task 1: complete (TextParsing + tests, review clean)
  - Minor: ParseNumber(null) 无显式用例；Regex `\s+` 未缓存。留给最终 review 定夺。
- Task 2: complete (ImageProcessing + tests, review clean)
  - 计划代码自相矛盾两处，implementer 修对：Crop 负起点缩宽高；FilterWhiteComponents 夹具改 4x4（8连通下真孤立）。生产在界内不受影响。
- Task 3: complete (GetNumberRecognizer 委托 + 修泄漏, review clean, 33/33)
- Task 4: complete (ScreenCapturer BuildCommand + 死锁修复; 1 Important 已修: CaptureToBitmap 同步读 stdout 改 CopyToAsync; 35/35)
  - Minor 留最终 review: ExecuteAdbCommand 重定向 stdout 但未排空；`.Result` 可用 GetAwaiter().GetResult()；超时抛错时 stderr task 未观察；BuildCommand null 分支未测（Task 6 补注释）
- Task 5: complete (坐标全覆盖 + FindAllMatches NMS; 计划 3 处 wart 已修: Theory→Fact / r.w→r.width / 常量模板→非零方差; 39/39)
- Task 6: complete (死代码清理; build 0 err, 39/39)
- Final review: Ready to merge = Yes. 1 Important (ExecuteAdbCommand 重定向 stdout 未排空) 已修 + .Result→GetAwaiter() 全扫 + 3 新测试。终态 42/42, build 0 err。
- 遗留 follow-up（非阻塞）: ExecuteAdbCommandWithOutput 零调用者(死代码); tessdata 复制依赖主项目 bin 输出、无 ProjectReference; GetText 中文 OCR 路径(战斗胜利)无自动回归; 超时抛错路径 async task 未观察; LocationinformationTests 缺 x>=0/y>=0 边界断言。
