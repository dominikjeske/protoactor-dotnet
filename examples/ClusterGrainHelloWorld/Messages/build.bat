﻿
protoc Protos.proto -I=. -I=../../../src --csharp_out=. --csharp_opt=file_extension=.g.cs --grpc_out . --plugin=protoc-gen-grpc=%userprofile%\.nuget\packages\Grpc.Tools\1.0.1\tools\windows_x64\grpc_csharp_plugin.exe
dotnet ..\..\..\protobuf\ProtoGrainGenerator\bin\Debug\netcoreapp1.1\protograin.dll Protos.proto Protos_protoactor.cs