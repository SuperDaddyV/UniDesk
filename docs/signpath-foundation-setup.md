# UniDesk SignPath Foundation 配置

## 目标

本指南适用于未来选择签名发布时的 UniDesk 自有 EXE、实际承载托管代码的 DLL 和最终安装包；签名私钥由 SignPath 的 HSM 管理，不进入本仓库、GitHub 日志或开发电脑。项目所有者分别明确批准的 `v2.1.0`、`v2.2.0` 与 `v2.2.1` 未签名正式版例外不使用本流程，也不得表述为 SignPath 已通过；这些例外不自动适用于后续版本。

SignPath Foundation 申请与条款：

- https://signpath.org/
- https://signpath.org/terms.html
- https://docs.signpath.io/trusted-build-systems/github

项目目前使用 MIT 许可证并已有公开 Release，但最终资格和项目审批仍由 SignPath Foundation 决定。

## 申请前公开材料

提交申请前，默认分支必须公开提供以下内容：

- 仓库首页包含 `## Code signing policy`、SignPath Foundation 资助声明，以及指向 `CODE_SIGNING_POLICY.md` 和 `PRIVACY.md` 的链接。
- `CODE_SIGNING_POLICY.md` 列明 Authors、Reviewers、Approvers、签名范围、人工批准流程和证书滥用报告入口。
- `PRIVACY.md` 如实披露本地存储、和风天气、Windows 定位和用户主动检查 GitHub 更新的数据流。
- 发布说明包含同样的代码签名政策和隐私政策入口。
- 主界面显示和风天气数据时，在展开态和收缩态均显示指向 `https://www.qweather.com` 的可见来源署名。

## 一次性人工配置

1. 申请 SignPath Foundation 开源项目签名，并将仓库设置为 `https://github.com/SuperDaddyV/UniDesk`。
2. 按 SignPath 指引安装其 GitHub App，只授权 UniDesk 仓库。
3. 在 SignPath 项目中创建发布签名策略以及下面两个 Artifact Configuration。
4. 创建仅有提交签名请求权限的 API Token。
5. 为 `main` 启用禁止强推、禁止删除且要求普通 CI 通过的默认分支保护或 Ruleset，并启用仓库的 Private vulnerability reporting。
6. 创建名为 `release-signing` 的 GitHub Environment，仅允许 `main` 部署；API Token 只作为该 Environment 的 Secret `SIGNPATH_API_TOKEN` 保存，不创建同名仓库级 Secret。
7. 在 `release-signing` Environment 中创建 Variables：
   - `SIGNPATH_ORGANIZATION_ID`
   - `SIGNPATH_PROJECT_SLUG`
   - `SIGNPATH_SIGNING_POLICY_SLUG`
   - `SIGNPATH_PAYLOAD_ARTIFACT_CONFIGURATION_SLUG`
   - `SIGNPATH_INSTALLER_ARTIFACT_CONFIGURATION_SLUG`
   - `SIGNPATH_EXPECTED_SIGNER_SUBJECT`：填写 SignPath Foundation 为 UniDesk 实际签发证书的完整 Subject；首个候选包可在隔离环境中用 `Get-AuthenticodeSignature` 核对后录入，不能猜测或只填写显示名称。

不得把这些实际值写入仓库文件。配置完成后，从 GitHub Actions 手动运行 `Build and sign release candidate`；工作流绑定 `release-signing` Environment，且仅允许从正式仓库的 `main` 分支执行。版本输入在任何构建或签名前必须通过严格的三段数字版本校验。

## 应用载荷 Artifact Configuration

GitHub Actions 上传目录时会生成 ZIP，因此根元素必须是 `zip-file`。该配置签署 UniDesk 自有的应用宿主和托管代码 DLL，并保留 ZIP 中的其它运行时文件和 `release-source.json`。第三方运行时文件不以 UniDesk 身份重新签名。

```xml
<artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
  <zip-file>
    <pe-file-set>
      <include path="App/UniDesk.exe" />
      <include path="App/UniDesk.dll" />
      <include path="App/UniDesk.Hardware.Contracts.dll" />
      <include path="HardwareService/UniDesk.HardwareService.exe" />
      <include path="HardwareService/UniDesk.HardwareService.dll" />
      <include path="HardwareService/UniDesk.Hardware.Contracts.dll" />
      <include path="HardwareRepair/UniDesk.HardwareRepair.exe" />
      <include path="HardwareRepair/UniDesk.HardwareRepair.dll" />
      <for-each>
        <authenticode-sign />
      </for-each>
    </pe-file-set>
  </zip-file>
</artifact-configuration>
```

## 安装包 Artifact Configuration

```xml
<artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
  <zip-file>
    <pe-file path="UniDesk_Setup_*.exe">
      <authenticode-sign />
    </pe-file>
  </zip-file>
</artifact-configuration>
```

## 固定发布顺序

1. 普通 CI 全部通过。
2. 手动签名工作流从一个干净的 Git 提交生成应用载荷。
3. 独立 `sign-payload` Runner 只向 SignPath 提交不可变 artifact id 并上传签名返回载荷，不检出或编译源码。
4. 新的 `build-installer` Runner 重新检出精确 `github.sha`，下载签名前／签名后载荷，验证签前清单 SHA、完整目录／文件清单、非签名文件 SHA-256、一方 PE 的 Authenticode 规范化内容哈希、签名和版本后编译安装包，并固化未签名安装包的规范化内容哈希。
5. 独立 `sign-installer` Runner 只向 SignPath 提交未签名安装包 artifact id 并上传签名返回安装包。
6. 新的 `verify-release-candidate` Runner 再次检出精确源码，重新下载全部所需 artifact；`Test-ReleaseReadiness.ps1` 验证源码提交、SDK 与锁文件、全部目录／文件清单、全部 UniDesk 一方文件的预期且一致签名者、签名安装包与未签名安装包的规范化内容哈希一致、PawnIO 固定哈希与上游签名者，并生成 `release-manifest.json`。
7. 人工核对发布测试矩阵。
8. 获得用户最终确认后，才能创建 Git tag 和 GitHub Release。

工作流只上传经过验证的候选制品，不会自动创建 Release。
