const phoneService = require('./phoneService');

/**
 * Bóc tách ngân sách từ tin nhắn của người dùng bằng Regex
 */
function extractBudget(text) {
  const lower = text.toLowerCase();
  let min = null;
  let max = null;

  // Dạng 1: "tầm 7-10tr", "từ 7 đến 10 triệu", "7 - 10 củ"
  const rangeMatch = lower.match(/(?:tầm|khoảng|từ)?\s*(\d+(?:[.,]\d+)?)\s*(?:-|đến|tới)\s*(\d+(?:[.,]\d+)?)\s*(?:tr|triệu|củ|m)/i);
  if (rangeMatch) {
    min = parseFloat(rangeMatch[1].replace(',', '.')) * 1000000;
    max = parseFloat(rangeMatch[2].replace(',', '.')) * 1000000;
    return { min, max };
  }

  // Dạng 2: "dưới 5 triệu", "< 10tr", "dưới 15 củ", "tối đa 20tr"
  const underMatch = lower.match(/(?:dưới|<|thấp hơn|tối đa|ko quá|không quá)\s*(\d+(?:[.,]\d+)?)\s*(?:tr|triệu|củ|m|k|nghìn)?/i);
  if (underMatch) {
    let val = parseFloat(underMatch[1].replace(',', '.'));
    if (!lower.includes('k') && !lower.includes('nghìn')) val *= 1000000;
    else val *= 1000;
    max = val;
    return { min: 0, max };
  }

  // Dạng 3: "trên 20 củ", "> 15tr", "từ 10 triệu trở lên"
  const overMatch = lower.match(/(?:trên|>|hơn|từ)\s*(\d+(?:[.,]\d+)?)\s*(?:tr|triệu|củ|m)?\s*(?:trở lên)?/i);
  if (overMatch && (lower.includes('trên') || lower.includes('>') || lower.includes('trở lên'))) {
    let val = parseFloat(overMatch[1].replace(',', '.'));
    val *= 1000000;
    min = val;
    return { min, max: null };
  }

  // Dạng 4: "tầm 10 triệu", "khoảng 8tr", "có 12 củ"
  const exactMatch = lower.match(/(?:tầm|khoảng|có|cỡ|giá)\s*(\d+(?:[.,]\d+)?)\s*(?:tr|triệu|củ|m)/i);
  if (exactMatch) {
    let val = parseFloat(exactMatch[1].replace(',', '.')) * 1000000;
    return { min: Math.max(0, val - 2000000), max: val + 2000000 };
  }

  return { min: null, max: null };
}

/**
 * Xác định nhu cầu sử dụng của khách hàng
 */
function detectNeeds(text) {
  const lower = text.toLowerCase();
  const needs = [];

  if (/(chơi game|cày game|gaming|genshin|pubg|liên quân|fps|mượt mà|hiệu năng|chip mạnh|đồ họa)/i.test(lower)) {
    needs.push('gaming');
  }
  if (/(chụp ảnh|sống ảo|quay video|camera|tiktok|selfie|chụp hình|zoom|chống rung|ois)/i.test(lower)) {
    needs.push('camera');
  }
  if (/(pin trâu|pin khủng|chạy grab|shipper|xe ôm|giao hàng|người lớn tuổi|pin lâu|dùng cả ngày)/i.test(lower)) {
    needs.push('battery');
  }
  if (/(sinh viên|học sinh|học tập|giá rẻ|phổ thông|tiết kiệm|vừa túi tiền)/i.test(lower)) {
    needs.push('student');
  }

  return needs;
}

/**
 * Trích xuất tên hãng nếu có
 */
function detectBrand(text) {
  const lower = text.toLowerCase();
  if (/(iphone|apple|ip)/i.test(lower)) return 'Apple';
  if (/(samsung|galaxy)/i.test(lower)) return 'Samsung';
  if (/(xiaomi|redmi|poco)/i.test(lower)) return 'Xiaomi';
  if (/(oppo|reno)/i.test(lower)) return 'OPPO';
  if (/(vivo)/i.test(lower)) return 'Vivo';
  return '';
}

/**
 * Bộ xử lý Offline Rule-Based
 */
async function processRuleBased(message) {
  const lower = message.toLowerCase();

  // 1. Chào hỏi
  if (/^(xin chào|chào|hello|hi|alo|chào shop|hey|bắt đầu)/i.test(lower.trim())) {
    return `Chào bạn! Mình là **KittyBot ✦** - Trợ lý tư vấn công nghệ chính thức của **KittyPhone**.

Mình có thể hỗ trợ bạn ngay:
- 📱 **Gợi ý chọn điện thoại** theo ngân sách và nhu cầu (Chơi game, chụp ảnh, pin trâu...)
- ⚡ **Tra cứu cấu hình chi tiết & tồn kho** các dòng iPhone, Galaxy, Xiaomi...
- 🎁 **Cập nhật chương trình khuyến mãi & Voucher giảm giá** mới nhất tại KittyPhone.

Bạn đang quan tâm đến dòng máy nào hoặc có ngân sách dự kiến bao nhiêu để mình tư vấn ngay nhé!`;
  }

  // 2. Hỏi về Khuyến mãi / Voucher
  if (/(khuyến mãi|khuyen mai|voucher|giảm giá|sale|ưu đãi|deal)/i.test(lower)) {
    const promos = await phoneService.getActivePromotions();
    if (!promos || promos.length === 0) {
      return `Hiện tại KittyPhone đang áp dụng chương trình **Trợ giá thu cũ đổi mới lên đến 2.500.000đ** và **Hỗ trợ trả góp 0%** toàn bộ các dòng máy! Bạn có thể xem thêm deal tại trang chủ nhé.`;
    }

    let response = `🎁 **CÁC CHƯƠNG TRÌNH KHUYẾN MÃI ĐANG DIỄN RA TẠI KITTYPHONE:**\n\n`;
    promos.forEach((p, idx) => {
      response += `${idx + 1}. **${p.name}**\n   - Ưu đãi: Giảm **${p.discount}**\n   - Nội dung: ${p.description || 'Áp dụng khi mua điện thoại tại cửa hàng'}\n\n`;
    });
    response += `💡 *Lưu ý: Khuyến mãi tự động áp dụng hoặc chọn mã voucher tại trang thanh toán của KittyPhone!*`;
    return response;
  }

  // 3. Hỏi kiểm tra tồn kho cụ thể
  if (/(còn hàng không|còn máy không|còn bao nhiêu|tồn kho|số lượng còn)/i.test(lower)) {
    // Tìm tên máy trong chuỗi
    const cleaned = message.replace(/(còn hàng không|còn máy không|tồn kho|kiểm tra|xem giúp|shop còn|cho hỏi|máy|chiếc)/gi, '').trim();
    if (cleaned.length >= 2) {
      const stock = await phoneService.checkStock({ productName: cleaned });
      if (stock && stock.length > 0) {
        let resp = `📦 **TÌNH TRẠNG TỒN KHO TẠI KITTYPHONE:**\n\n`;
        stock.forEach(item => {
          resp += `• **${item.productName}** (${item.brand})\n  - Giá: **${item.priceFormatted}**\n  - Trạng thái: **${item.status}** (${item.quantity} máy sẵn sàng giao)\n\n`;
        });
        resp += `👉 Bạn có thể bấm **Mua ngay** hoặc đặt cọc online trên website để giữ máy nhé!`;
        return resp;
      }
    }
  }

  // 4. So sánh 2 máy (VD: "so sánh iPhone 15 và Galaxy S25")
  const compareMatch = message.match(/so sánh\s+([A-Za-z0-9\s]+?)\s+(?:và|voi|với|vs)\s+([A-Za-z0-9\s]+)/i);
  if (compareMatch) {
    const phoneA = compareMatch[1].trim();
    const phoneB = compareMatch[2].trim();
    const result = await phoneService.comparePhones({ phoneA, phoneB });

    if (result.phoneA && result.phoneB) {
      let resp = `⚖️ **SO SÁNH ${result.phoneA.productName || phoneA} VS ${result.phoneB.productName || phoneB}:**\n\n`;
      resp += `1. **${result.phoneA.productName}**\n`;
      resp += `   - Giá bán: **${result.phoneA.priceFormatted}** (Kho: ${result.phoneA.quantity} máy)\n`;
      if (result.phoneA.specs?.cpu) resp += `   - Vi xử lý: ${result.phoneA.specs.cpu}\n`;
      if (result.phoneA.specs?.screen) resp += `   - Màn hình: ${result.phoneA.specs.screen}\n`;
      if (result.phoneA.specs?.battery) resp += `   - Pin & Sạc: ${result.phoneA.specs.battery}\n\n`;

      resp += `2. **${result.phoneB.productName}**\n`;
      resp += `   - Giá bán: **${result.phoneB.priceFormatted}** (Kho: ${result.phoneB.quantity} máy)\n`;
      if (result.phoneB.specs?.cpu) resp += `   - Vi xử lý: ${result.phoneB.specs.cpu}\n`;
      if (result.phoneB.specs?.screen) resp += `   - Màn hình: ${result.phoneB.specs.screen}\n`;
      if (result.phoneB.specs?.battery) resp += `   - Pin & Sạc: ${result.phoneB.specs.battery}\n\n`;

      resp += `💡 **Góc tư vấn:** Nếu bạn thích hệ sinh thái mượt mà lâu dài, ưu tiên Apple; còn thích màn hình xuất sắc và đa nhiệm đỉnh cao, hãy chọn Samsung Galaxy!`;
      return resp;
    }
  }

  // 5. Tìm kiếm theo Ngân sách & Nhu cầu (Core Rule-Based Engine)
  const budget = extractBudget(message);
  const needs = detectNeeds(message);
  const brand = detectBrand(message);

  let searchKeyword = '';
  // Nếu có từ khóa dòng máy cụ thể (VD: "iPhone 15", "Galaxy S24", "Xiaomi 14")
  const modelMatch = message.match(/(iphone\s*\d+\s*(?:pro\s*max|pro|plus)?|galaxy\s*[a-z0-9\s]+|xiaomi\s*[a-z0-9\s]+|redmi\s*[a-z0-9\s]+)/i);
  if (modelMatch) {
    searchKeyword = modelMatch[0].trim();
  }

  const phones = await phoneService.searchPhones({
    keyword: searchKeyword,
    brand: brand,
    minPrice: budget.min,
    maxPrice: budget.max,
    limit: 6
  });

  if (phones && phones.length > 0) {
    let headerText = `✨ **KITTYBOT XIN GỢI Ý CÁC MẪU ĐIỆN THOẠI PHÙ HỢP:**\n`;
    if (budget.min || budget.max) {
      headerText += `💰 Ngân sách: ${budget.min ? phoneService.formatCurrency(budget.min) : '0đ'} - ${budget.max ? phoneService.formatCurrency(budget.max) : 'Không giới hạn'}\n`;
    }
    if (needs.length > 0) {
      const needLabels = {
        gaming: '🎮 Cày game & Hiệu năng cao',
        camera: '📸 Chụp ảnh đẹp & Sống ảo',
        battery: '🔋 Pin trâu & Chạy xe công nghệ / tác vụ dài',
        student: '🎓 Học tập & Phổ thông giá tốt'
      };
      headerText += `🎯 Tiêu chí: ${needs.map(n => needLabels[n]).join(', ')}\n`;
    }
    headerText += `\n`;

    let body = '';
    phones.forEach((p, idx) => {
      body += `**${idx + 1}. ${p.productName}**\n`;
      body += `• Giá bán chính hãng: **${p.priceFormatted}**\n`;
      body += `• Tình trạng: ${p.inStock ? `✅ Còn hàng (${p.quantity} máy)` : '❌ Tạm hết hàng'}\n`;
      if (p.specs?.cpu) body += `• Cấu hình: ${p.specs.cpu}\n`;
      if (p.specs?.battery) body += `• Pin & Sạc: ${p.specs.battery}\n`;
      if (p.specs?.camera && needs.includes('camera')) body += `• Camera: ${p.specs.camera}\n`;
      body += `\n`;
    });

    body += `🛒 *Tất cả sản phẩm đều được bảo hành chính hãng 12 tháng, 1 đổi 1 trong 30 ngày và hỗ trợ trả góp 0% tại KittyPhone!*`;
    return headerText + body;
  }

  // 6. Câu hỏi ngoài lề (Quy tắc Chuyên viên KittyPhone)
  if (/(làm thơ|giải toán|phương trình|viết code|javascript|python|chính trị|bóng đá|thời tiết|nấu ăn|du lịch|phim|nhạc|bài hát|kể chuyện|truyện cười|tử vi|bói toán|dịch thuật|viết email|viết thư|thể thao|gym|chứng khoán|bitcoin|pháp luật)/i.test(lower)) {
    return `Dạ, mình là **Chuyên viên tư vấn công nghệ tại KittyPhone** nên chỉ có thể hỗ trợ bạn các vấn đề liên quan đến:

📱 **Chọn mua điện thoại** chính hãng theo nhu cầu và ngân sách
⚡ **Tra cứu cấu hình**, thông số kỹ thuật, so sánh hiệu năng các dòng máy
📦 **Kiểm tra tồn kho** và tình trạng sản phẩm tại cửa hàng
🎁 **Cập nhật khuyến mãi** và chương trình ưu đãi hiện hành

Bạn hãy chia sẻ nhu cầu về điện thoại hoặc tầm ngân sách mong muốn để mình hỗ trợ nhé! 😊`;
  }

  // Fallback thân thiện
  return `Chào bạn! Mình có thể hỗ trợ bạn tìm mẫu điện thoại nào tại KittyPhone? Bạn có thể gửi cho mình tầm ngân sách (ví dụ: *tầm 10 triệu*, *dưới 7tr*) hoặc nhu cầu sử dụng (*chơi game*, *chụp ảnh đẹp*, *pin trâu*) nhé!`;
}

module.exports = {
  extractBudget,
  detectNeeds,
  detectBrand,
  processRuleBased
};
