# kaspadotnet

Fork of [svarogg/kaspadotnet](https://github.com/svarogg/kaspadotnet) by [composehk](https://github.com/composehk).

This is a tiny example on how to use the RPCs for kaspad and kaspawallet from a dotnet project.

## Role in the dual-repo plan

与 [composehk/kaswallet](https://github.com/composehk/kaswallet)（`dotnet/` C# 钱包库 + CLI）**互补**：

| 仓库 | 侧重点 |
|------|--------|
| **kaswallet** | 钱包核心：地址校验、REST 余额/UTXO、BIP39、未签名交易草稿、CLI |
| **kaspadotnet** | .NET 调 kaspad / kaspawallet **RPC/gRPC 客户端与示例**，偏节点与协议集成 |

两仓共享经验与修复；本仓后续会扩展为更完整的 Kaspa .NET API/SDK 示例层。

## Instructions
1. Make sure you have the latest versions of kaspad and kaspawallet installed.
2. Run `kaspad --utxoindex` in a separate terminal.
3. Run `kaspawallet start-daemon` in a separate terminal.
4. Run `dotnet run --project KaspaDotnetExamples` from the repository's root
