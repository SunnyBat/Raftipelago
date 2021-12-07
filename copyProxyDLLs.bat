Rem @ECHO OFF
Rem copy /B /Y /L ArchipelagoProxybinDebug\Archipelago.MultiClient.Net.dll RaftipelagoData\Archipelago.MultiClient.Net.dll
Rem copy /B /Y /L ArchipelagoProxybinDebug\ArchipelagoProxy.dll RaftipelagoData\ArchipelagoProxy.dll
Rem copy /B /Y /L ArchipelagoProxybinDebug\websocket-sharp.dll RaftipelagoData\\websocket-sharp.dll
ECHO %1
ECHO %2
copy /B /Y /L %1Archipelago.MultiClient.Net.dll %2Archipelago.MultiClient.Net.dll
copy /B /Y /L %1ArchipelagoProxy.dll %2ArchipelagoProxy.dll
copy /B /Y /L %1websocket-sharp.dll %2\websocket-sharp.dll