namespace AdminSite.Services.Notifications;

public static class AdminOutcomeTextResolver
{
    private static readonly IReadOnlyDictionary<string, string> ReasonAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage-unavailable"] = "service-unavailable",
            ["resource-not-found"] = "not-found",
            ["too-many-active-uploads"] = "rate-limited",
            ["invalid-upload-context"] = "unsupported-value",
            ["upload-expired"] = "expired"
        };

    private static readonly IReadOnlyDictionary<string, (string En, string Vi, string Cn)> Entities =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin-user"] = ("User", "Tài khoản", "用户"),
            ["user"] = ("User", "Tài khoản", "用户"),
            ["role"] = ("Role", "Vai trò", "角色"),
            ["remembered-device"] = ("Remembered device", "Thiết bị đã ghi nhớ", "已记住的设备"),
            ["page"] = ("Page", "Page", "Page"),
            ["child-page"] = ("Child Page", "Page con", "子 Page"),
            ["section"] = ("Section", "Section", "Section"),
            ["block"] = ("Block", "Block", "Block"),
            ["section-preset"] = ("Section preset", "Mẫu Section", "Section 预设"),
            ["content"] = ("Content", "Nội dung", "内容"),
            ["form"] = ("Form", "Form", "Form"),
            ["form-definition"] = ("Form definition", "Định nghĩa Form", "Form 定义"),
            ["form-input-type"] = ("Form input type", "Loại trường Form", "Form 输入类型"),
            ["form-submission"] = ("Form submission", "Lượt gửi Form", "Form 提交记录"),
            ["submission"] = ("Submission", "Lượt gửi", "提交记录"),
            ["content-type"] = ("Content type", "Loại nội dung", "内容类型"),
            ["asset"] = ("Asset", "Tài nguyên", "资源"),
            ["resource"] = ("Resource", "Tài nguyên", "资源"),
            ["resource-album"] = ("Resource album", "Album tài nguyên", "资源相册"),
            ["resource-upload"] = ("Upload", "Tải lên", "上传"),
            ["storage-migration"] = ("Storage migration", "Chuyển đổi lưu trữ", "存储迁移"),
            ["settings"] = ("Settings", "Cài đặt", "设置"),
            ["language-settings"] = ("Language settings", "Cài đặt ngôn ngữ", "语言设置"),
            ["admin-appearance"] = ("Admin appearance", "Giao diện Admin", "管理界面"),
            ["resource-library-settings"] = ("Resource Library settings", "Cài đặt Thư viện tài nguyên", "资源库设置"),
            ["glossary-term"] = ("Glossary term", "Thuật ngữ", "术语"),
            ["theme"] = ("Theme", "Giao diện", "主题"),
            ["branding"] = ("Branding", "Nhận diện", "品牌设置"),
            ["global-button"] = ("Global Button", "Nút toàn cục", "全局按钮"),
            ["footer"] = ("Footer", "Footer", "Footer"),
            ["footer-group"] = ("Footer group", "Nhóm Footer", "Footer 分组"),
            ["footer-link"] = ("Footer link", "Liên kết Footer", "Footer 链接"),
            ["social-button"] = ("Social button", "Nút mạng xã hội", "社交按钮"),
            ["social-button-group"] = ("Social button group", "Nhóm nút mạng xã hội", "社交按钮组"),
            ["block-graph"] = ("Block group", "Nhóm Block", "Block 组"),
            ["visitor-metrics"] = ("Website activity", "Hoạt động Website", "网站活动"),
            ["log-management"] = ("Log management", "Quản lý nhật ký", "日志管理"),
            ["log"] = ("Log operation", "Thao tác nhật ký", "日志操作")
        };

    private static readonly IReadOnlyDictionary<string, (string En, string Vi, string Cn)> Reasons =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["authentication-required"] = ("your session is no longer valid", "phiên đăng nhập không còn hợp lệ", "登录会话已失效"),
            ["permission-denied"] = ("your account does not have permission", "tài khoản không có quyền", "当前账户没有权限"),
            ["not-found"] = ("the requested item no longer exists", "mục yêu cầu không còn tồn tại", "请求的项目已不存在"),
            ["source-page-not-found"] = ("the selected source Page no longer exists", "Page nguồn đã chọn không còn tồn tại", "所选源 Page 已不存在"),
            ["conflict"] = ("the data changed before the operation completed", "dữ liệu đã thay đổi trước khi thao tác hoàn tất", "操作完成前数据已发生变化"),
            ["expired"] = ("the operation expired", "thao tác đã hết hạn", "操作已过期"),
            ["rate-limited"] = ("too many requests are currently active", "hiện có quá nhiều yêu cầu", "当前请求过多"),
            ["in-use"] = ("the item is currently in use", "mục này đang được sử dụng", "该项目正在使用中"),
            ["locked"] = ("the item is locked", "mục này đang bị khóa", "该项目已锁定"),
            ["size-exceeded"] = ("the selected file exceeds the allowed size", "tệp đã chọn vượt quá kích thước cho phép", "所选文件超过允许大小"),
            ["unsupported-value"] = ("one or more values are not supported", "một hoặc nhiều giá trị không được hỗ trợ", "一个或多个值不受支持"),
            ["validation-failed"] = ("one or more values are invalid", "một hoặc nhiều giá trị không hợp lệ", "一个或多个值无效"),
            ["request-timeout"] = ("the request timed out", "yêu cầu đã hết thời gian chờ", "请求超时"),
            ["service-unavailable"] = ("the required service is temporarily unavailable", "dịch vụ cần thiết tạm thời không khả dụng", "所需服务暂时不可用"),
            ["unexpected-error"] = ("an unexpected system error occurred", "đã xảy ra lỗi hệ thống không mong muốn", "发生了意外系统错误"),
            ["operation-failed"] = ("the operation could not be completed", "không thể hoàn tất thao tác", "无法完成操作"),
            ["invalid-signature"] = ("the file content does not match an allowed format", "nội dung tệp không khớp định dạng được cho phép", "文件内容与允许的格式不匹配"),
            ["unsafe-image-dimensions"] = ("the image dimensions are unsafe", "kích thước ảnh không an toàn", "图像尺寸不安全"),
            ["size-mismatch"] = ("the uploaded file size could not be verified", "không thể xác minh kích thước tệp đã tải lên", "无法验证上传文件的大小"),
            ["content-type-mismatch"] = ("the uploaded file type could not be verified", "không thể xác minh loại tệp đã tải lên", "无法验证上传文件的类型"),
            ["object-missing"] = ("the uploaded file was not found in storage", "không tìm thấy tệp đã tải lên trong lưu trữ", "存储中未找到上传的文件"),
            ["upload-not-found"] = ("the upload session no longer exists", "phiên tải lên không còn tồn tại", "上传会话已不存在"),
            ["initiation-failed"] = ("storage could not start the transfer", "lưu trữ không thể bắt đầu truyền tệp", "存储无法开始传输"),
            ["verification-failed"] = ("the uploaded file could not be verified", "không thể xác minh tệp đã tải lên", "无法验证上传的文件"),
            ["resource-creation-failed"] = ("the Resource Library record could not be created", "không thể tạo bản ghi Thư viện tài nguyên", "无法创建资源库记录"),
            ["storage-key-missing"] = ("storage could not finish the transfer", "lưu trữ không thể hoàn tất truyền tệp", "存储无法完成传输"),
            ["direct-upload-disabled"] = ("direct uploads are disabled in Settings", "tải lên trực tiếp đang bị tắt trong Cài đặt", "设置中已关闭直接上传"),
            ["replacement-in-progress"] = ("another replacement of this resource is still running", "một lần thay thế khác cho tài nguyên này vẫn đang chạy", "此资源的另一次替换仍在进行"),
            ["upload-in-progress"] = ("this upload is already being completed", "lần tải lên này đang được hoàn tất", "此上传正在完成"),
            ["invalid-upload-state"] = ("this upload has already finished or changed state", "lần tải lên này đã hoàn tất hoặc đổi trạng thái", "此上传已完成或状态已更改"),
            ["resource-reconciliation-failed"] = ("the completed Resource Library item could not be found", "không tìm thấy mục Thư viện tài nguyên đã hoàn tất", "找不到已完成的资源库项目"),
            ["registry-reconciliation-required"] = ("the Resource Library record is still being registered", "bản ghi Thư viện tài nguyên vẫn đang được đăng ký", "资源库记录仍在注册中")
        };

    public static string ResolveArgument(string value, string language)
    {
        if (value.StartsWith("@action:", StringComparison.OrdinalIgnoreCase))
            return ResolveAction(value[8..], language);
        if (value.StartsWith("@reason:", StringComparison.OrdinalIgnoreCase))
            return ResolveReason(value[8..], language);
        return value;
    }

    private static string ResolveAction(string actionCode, string language)
    {
        var parts = actionCode.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        var entityCode = parts.FirstOrDefault() ?? "operation";
        var verbCode = parts.Length > 1 ? parts[1] : "changed";
        var entity = Pick(Entities.GetValueOrDefault(entityCode, ("Operation", "Thao tác", "操作")), language);
        var operation = Operation(verbCode, language);
        return language switch
        {
            "vi" => $"{operation} {entity}",
            "cn" => $"{entity}{operation}",
            _ => $"{entity} {operation}"
        };
    }

    private static string ResolveReason(string errorCode, string language)
    {
        var normalized = errorCode.Trim().ToLowerInvariant().Replace('_', '-');
        if (ReasonAliases.TryGetValue(normalized, out var canonical))
            normalized = canonical;
        return Pick(Reasons.GetValueOrDefault(normalized, Reasons["operation-failed"]), language);
    }

    private static string Operation(string value, string language)
    {
        var code = value.ToLowerInvariant();
        var operation = code.Contains("delete") ? ("deletion", "xóa", "删除") :
            code.Contains("reorder") ? ("reorder", "sắp xếp", "排序") :
            code.Contains("publish") ? ("publish", "xuất bản", "发布") :
            code.Contains("restore") ? ("restore", "khôi phục", "恢复") :
            code.Contains("reset") ? ("reset", "đặt lại", "重置") :
            code.Contains("replace") ? ("replacement", "thay thế", "替换") :
            code.Contains("cancel") || code.Contains("abort") ? ("cancellation", "hủy", "取消") :
            code.Contains("revoke") ? ("revocation", "thu hồi", "撤销") :
            code.Contains("approve") ? ("approval", "phê duyệt", "批准") :
            code.Contains("reject") ? ("rejection", "từ chối", "拒绝") :
            code.Contains("submit") ? ("submission", "gửi", "提交") :
            code.Contains("upload") ? ("upload", "tải lên", "上传") :
            code.Contains("duplicate") ? ("duplication", "nhân bản", "复制") :
            code.Contains("move") ? ("move", "di chuyển", "移动") :
            code.Contains("visibility") ? ("visibility update", "cập nhật hiển thị", "可见性更新") :
            code.Contains("access") ? ("access update", "cập nhật truy cập", "访问权限更新") :
            code.Contains("password") ? ("password update", "cập nhật mật khẩu", "密码更新") :
            code.Contains("create") || code.Contains("add") ? ("creation", "tạo", "创建") :
            code.Contains("export") ? ("export", "xuất dữ liệu", "导出") :
            ("save", "lưu", "保存");
        return Pick(operation, language);
    }

    private static string Pick((string En, string Vi, string Cn) value, string language) => language switch
    {
        "vi" => value.Vi,
        "cn" => value.Cn,
        _ => value.En
    };
}
