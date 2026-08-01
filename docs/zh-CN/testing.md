# 测试说明

核心检查：

```text
python tools/ci/run_checks.py --profile pr
```

发布检查还会验证维护者/安全联络配置、Changelog、第三方资产排除和两次构建
哈希一致性：

```text
python tools/ci/run_checks.py --profile release
```

Unity 与 Blender 必须分别用精确编辑器运行，不能因为入口存在就写“已通过”。
当前环境没有 Blender，因此 Blender headless 只能标记为“入口已实现、未执行”。
完整命令见英文 [`testing.md`](../development/testing.md)。
