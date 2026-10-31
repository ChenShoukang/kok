$ErrorActionPreference='Stop'
[Console]::InputEncoding=New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding $false
while($true){
    $line=[Console]::In.ReadLine()
    if($null -eq $line){break}
    if($line.Trim().Length -eq 0){continue}
    try{$message=$line|ConvertFrom-Json}catch{continue}
    if($null -eq $message.id){continue}
    $reply=@{jsonrpc='2.0';id=$message.id}
    switch([string]$message.method){
        'server/discover'{ $reply.error=@{code=-32601;message='method not found'} }
        'initialize'{ $reply.result=@{protocolVersion='2025-11-25';capabilities=@{};serverInfo=@{name='sandbox-fixture';version='1'}} }
        'tools/list'{
            $schema=@{type='object';properties=@{text=@{type='string'}};required=@('text')}
            $reply.result=@{tools=@(@{name='echo';description='Echo text';inputSchema=$schema})}
        }
        'tools/call'{
            $text=''
            if($null -ne $message.params -and $null -ne $message.params.arguments -and $null -ne $message.params.arguments.text){$text=[string]$message.params.arguments.text}
            $reply.result=@{content=@(@{type='text';text='SANDBOX-ECHO:'+$text})}
        }
        default{ $reply.error=@{code=-32601;message='method not found'} }
    }
    [Console]::Out.WriteLine(($reply|ConvertTo-Json -Compress -Depth 10))
    [Console]::Out.Flush()
}
