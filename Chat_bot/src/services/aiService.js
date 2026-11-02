const Groq = require('groq-sdk');
const phoneService = require('./phoneService');
const ruleBasedService = require('./ruleBasedService');

const SYSTEM_PROMPT = `Bạn là KittyBot ✦ - Chuyên viên tư vấn công nghệ và thiết bị di động chính thức tại hệ thống cửa hàng KittyPhone (chuỗi bán lẻ điện thoại cao cấp).

QUY TẮC BẮT BUỘC TUYỆT ĐỐI - KHÔNG ĐƯỢC VI PHẠM:

1. PHẠM VI TRẢ LỜI: Bạn CHỈ được phép trả lời các câu hỏi liên quan đến điện thoại, phụ kiện công nghệ, và dịch vụ của KittyPhone. Với MỌI câu hỏi ngoài phạm vi (thơ, toán, code, thời tiết, chính trị, bóng đá, lịch sử, y tế, tình cảm, nấu ăn, du lịch, v.v.), bạn PHẢI từ chối lịch sự và hướng khách quay lại chủ đề điện thoại.

2. TUYỆT ĐỐI KHÔNG BỊA ĐẶT: Bạn KHÔNG ĐƯỢC tự nghĩ ra, bịa đặt, hay suy đoán bất kỳ thông tin nào về sản phẩm (giá cả, thông số, tồn kho, khuyến mãi). BẮT BUỘC phải gọi Function Calling để tra cứu dữ liệu thực tế từ hệ thống CSDL KittyPhone TRƯỚC KHI trả lời.

3. KHI KHÔNG TÌM THẤY DỮ LIỆU: Nếu function trả về mảng rỗng [], null, hoặc không có kết quả, bạn PHẢI trả lời trung thực: "Hiện tại KittyPhone chưa có sản phẩm này trong kho" hoặc "Mình chưa tìm thấy thông tin phù hợp". TUYỆT ĐỐI KHÔNG tự bịa ra sản phẩm, giá, hay thông số thay thế.

4. CHỈ DÙNG DỮ LIỆU TỪ FUNCTION: Mọi thông tin về tên sản phẩm, giá bán, thông số kỹ thuật, số lượng tồn kho, khuyến mãi trong câu trả lời PHẢI đến từ kết quả Function Calling. Không thêm thông số, giá, hoặc tính năng mà dữ liệu trả về không có.

5. QUY TẮC TÌM KIẾM THEO NHU CẦU: Khi khách hỏi theo nhu cầu (chơi game, chụp ảnh đẹp, pin trâu, tầm giá 10-15tr...), dùng tham số 'minPrice', 'maxPrice' và 'brand' trong hàm 'searchPhones'. KHÔNG điền các từ miêu tả như "chơi game", "gaming" vào tham số 'keyword'. Sau khi có kết quả, đọc thông số thực tế để tư vấn.

6. QUY TẮC HIỂN THỊ: KHÔNG BAO GIỜ in mã thẻ kỹ thuật như <tool_call>, <function>, <parameter> ra câu trả lời. Luôn trả lời bằng văn bản tiếng Việt tự nhiên, ấm áp, chuyên nghiệp.

7. TRÌNH BÀY: Dùng các icon công nghệ (📱, ⚡, 🎮, 📸, 🔋, 💰, 🛒), xuống dòng rõ ràng, in đậm markdown (**tên máy**, **giá tiền**).

8. CHÍNH SÁCH: Khi phù hợp, nhắc đến: Bảo hành chính hãng 12 tháng, 1 đổi 1 trong 30 ngày, trả góp 0% tại KittyPhone.

9. KHÔNG THÊM THÔNG TIN NGOÀI DỮ LIỆU: Không tự thêm đánh giá benchmark, điểm AnTuTu, so sánh với đối thủ, hoặc bất kỳ thông tin nào không có trong kết quả function. Chỉ mô tả đúng những gì dữ liệu trả về.`;

const TOOLS = [
  {
    type: 'function',
    function: {
      name: 'searchPhones',
      description: 'Tìm kiếm điện thoại theo tên mẫu máy, hãng, hoặc khoảng giá trong cơ sở dữ liệu KittyPhone',
      parameters: {
        type: 'object',
        properties: {
          keyword: { type: 'string', description: 'Tên mẫu máy (VD: iPhone 15, Galaxy S25, Xiaomi 14, Reno 12). KHÔNG nhập các từ mô tả như "gaming", "pin trâu" vào đây.' },
          brand: { type: 'string', description: 'Tên hãng (Apple, Samsung, Xiaomi, OPPO, Vivo)' },
          minPrice: { type: 'number', description: 'Ngân sách tối thiểu (VNĐ)' },
          maxPrice: { type: 'number', description: 'Ngân sách tối đa (VNĐ)' }
        }
      }
    }
  },
  {
    type: 'function',
    function: {
      name: 'getPhoneSpecs',
      description: 'Lấy thông số kỹ thuật chi tiết (Chip xử lý, RAM, Màn hình, Camera, Dung lượng Pin) của một mẫu máy cụ thể',
      parameters: {
        type: 'object',
        properties: {
          productName: { type: 'string', description: 'Tên đầy đủ hoặc viết tắt của mẫu điện thoại' }
        },
        required: ['productName']
      }
    }
  },
  {
    type: 'function',
    function: {
      name: 'checkStock',
      description: 'Kiểm tra số lượng máy thực tế còn trong kho tại cửa hàng KittyPhone',
      parameters: {
        type: 'object',
        properties: {
          productName: { type: 'string', description: 'Tên điện thoại cần kiểm tra' }
        },
        required: ['productName']
      }
    }
  },
  {
    type: 'function',
    function: {
      name: 'comparePhones',
      description: 'So sánh chi tiết cấu hình, giá bán và ưu nhược điểm giữa hai dòng máy',
      parameters: {
        type: 'object',
        properties: {
          phoneA: { type: 'string', description: 'Tên dòng máy thứ nhất' },
          phoneB: { type: 'string', description: 'Tên dòng máy thứ hai' }
        },
        required: ['phoneA', 'phoneB']
      }
    }
  },
  {
    type: 'function',
    function: {
      name: 'getActivePromotions',
      description: 'Lấy danh sách các chương trình khuyến mãi, voucher và giảm giá đang áp dụng tại KittyPhone',
      parameters: {
        type: 'object',
        properties: {}
      }
    }
  }
];

const OFF_TOPIC_PATTERNS = [
  /làm thơ|viết thơ|sáng tác thơ|bài thơ/i,
  /giải toán|phương trình|tính toán|đạo hàm|tích phân|hình học/i,
  /viết code|lập trình|javascript|python|java|html|css|c\+\+|react|nodejs/i,
  /chính trị|tổng thống|bầu cử|đảng|quốc hội/i,
  /bóng đá|world cup|ngoại hạng|champions|messi|ronaldo|euro/i,
  /thời tiết|nhiệt độ|mưa|nắng|bão|dự báo/i,
  /lịch sử|chiến tranh|triều đại|vua|hoàng đế/i,
  /y tế|bệnh|thuốc|triệu chứng|bác sĩ|covid|sức khỏe/i,
  /nấu ăn|công thức|món ăn|recipe|gia vị/i,
  /du lịch|khách sạn|resort|vé máy bay|tour/i,
  /tình cảm|yêu|chia tay|người yêu|crush|hẹn hò/i,
  /kể chuyện|truyện cười|joke|hài/i,
  /tâm linh|bói toán|tử vi|phong thủy|cung hoàng đạo/i,
  /dịch thuật|dịch sang|translate/i,
  /viết email|viết thư|soạn văn bản|viết bài|viết luận/i,
  /phim|diễn viên|ca sĩ|nhạc|bài hát|mv|âm nhạc/i,
  /động vật|thú cưng|chó|mèo|cá|chim/i,
  /thể thao|gym|tập luyện|yoga|chạy bộ/i,
  /tài chính|chứng khoán|bitcoin|crypto|cổ phiếu|forex/i,
  /pháp luật|luật|quy định|nghị định|hợp đồng/i,
  /triết học|ý nghĩa cuộc sống|tâm lý/i
];

const OFF_TOPIC_RESPONSE = `Dạ, mình là **Chuyên viên tư vấn công nghệ tại KittyPhone** nên chỉ có thể hỗ trợ bạn các vấn đề liên quan đến:

📱 **Chọn mua điện thoại** chính hãng theo nhu cầu và ngân sách
⚡ **Tra cứu cấu hình**, thông số kỹ thuật, so sánh hiệu năng các dòng máy
📦 **Kiểm tra tồn kho** và tình trạng sản phẩm tại cửa hàng
🎁 **Cập nhật khuyến mãi** và chương trình ưu đãi hiện hành

Bạn hãy chia sẻ nhu cầu về điện thoại hoặc tầm ngân sách mong muốn để mình hỗ trợ nhé! 😊`;

function isOffTopic(message) {
  const lower = message.toLowerCase();
  for (const pattern of OFF_TOPIC_PATTERNS) {
    if (pattern.test(lower)) return true;
  }
  return false;
}

function stripToolCallXml(text) {
  if (!text) return '';
  return text
    .replace(/<tool_call>[\s\S]*?<\/tool_call>/gi, '')
    .replace(/<\/?(function|parameter|tool_call)[^>]*>/gi, '')
    .replace(/<\|.*?\|>/g, '')
    .trim();
}

function parseTextToolCalls(content) {
  if (!content) return [];
  const calls = [];
  const toolCallRegex = /<tool_call>[\s\S]*?<function=(\w+)>([\s\S]*?)<\/function>[\s\S]*?<\/tool_call>/gi;
  let match;
  while ((match = toolCallRegex.exec(content)) !== null) {
    const fnName = match[1];
    const body = match[2];
    const paramRegex = /<parameter=(\w+)>([\s\S]*?)<\/parameter>/gi;
    let pMatch;
    const args = {};
    while ((pMatch = paramRegex.exec(body)) !== null) {
      const key = pMatch[1];
      let val = pMatch[2].trim();
      if (!isNaN(val) && val !== '') {
        val = Number(val);
      }
      args[key] = val;
    }
    calls.push({ name: fnName, args });
  }
  return calls;
}

async function executeFunctionCall(name, args) {
  switch (name) {
    case 'searchPhones':
      return await phoneService.searchPhones(args || {});
    case 'getPhoneSpecs':
      return await phoneService.getPhoneSpecs(args || {});
    case 'checkStock':
      return await phoneService.checkStock(args || {});
    case 'comparePhones':
      return await phoneService.comparePhones(args || {});
    case 'getActivePromotions':
      return await phoneService.getActivePromotions();
    default:
      return { error: `Function ${name} not found` };
  }
}

function sanitizeAiReply(reply, functionResults) {
  if (!reply) return reply;

  let cleaned = stripToolCallXml(reply);

  if (!functionResults || functionResults.length === 0) {
    return cleaned;
  }

  const allProductNames = new Set();
  const allPrices = new Set();

  for (const result of functionResults) {
    if (Array.isArray(result)) {
      for (const item of result) {
        if (item.productName) allProductNames.add(item.productName.toLowerCase());
        if (item.price) allPrices.add(item.price);
      }
    } else if (result && typeof result === 'object') {
      if (result.productName) allProductNames.add(result.productName.toLowerCase());
      if (result.price) allPrices.add(result.price);
      if (result.phoneA?.productName) allProductNames.add(result.phoneA.productName.toLowerCase());
      if (result.phoneB?.productName) allProductNames.add(result.phoneB.productName.toLowerCase());
      if (result.phoneA?.price) allPrices.add(result.phoneA.price);
      if (result.phoneB?.price) allPrices.add(result.phoneB.price);
    }
  }

  return cleaned;
}

async function processChatMessage(history = [], userMessage = '') {
  // Tier 0: Lọc câu hỏi ngoài lề (Off-Topic Guardrail)
  if (isOffTopic(userMessage)) {
    return {
      reply: OFF_TOPIC_RESPONSE,
      engine: 'KittyBot Content Filter',
      tier: 0
    };
  }

  const apiKey = (process.env.GROQ_API_KEY || '').trim();
  const modelName = process.env.GROQ_MODEL || 'qwen/qwen3.8-27b';

  if (!apiKey || apiKey.startsWith('gsk_your') || apiKey === '') {
    console.log('[AI Coordinator] Chưa có Groq API Key hợp lệ. Chuyển hướng sang Tầng 2 (Rule-Based Engine Offline).');
    const offlineReply = await ruleBasedService.processRuleBased(userMessage);
    return {
      reply: offlineReply,
      engine: 'Rule-Based Engine (Offline 0đ)',
      tier: 2
    };
  }

  try {
    const groq = new Groq({ apiKey });

    const messages = [
      { role: 'system', content: SYSTEM_PROMPT },
      ...history.slice(-6).map(m => ({ role: m.role, content: m.content })),
      { role: 'user', content: userMessage }
    ];

    console.log(`[AI Coordinator] Gọi Groq AI (${modelName}) với Function Calling...`);

    let iterations = 0;
    const MAX_ITERATIONS = 3;
    let usedTools = false;
    const collectedFunctionResults = [];

    while (iterations < MAX_ITERATIONS) {
      iterations++;

      const response = await groq.chat.completions.create({
        model: modelName,
        messages: messages,
        tools: TOOLS,
        tool_choice: 'auto',
        temperature: 0.3,
        max_tokens: 1024
      });

      const responseMessage = response.choices[0].message;
      let toolCallsToExecute = [];

      // 1. Kiểm tra tool_calls chuẩn (OpenAI JSON format)
      if (responseMessage.tool_calls && responseMessage.tool_calls.length > 0) {
        for (const tc of responseMessage.tool_calls) {
          let functionArgs = {};
          try {
            functionArgs = JSON.parse(tc.function.arguments);
          } catch (e) {
            functionArgs = {};
          }
          toolCallsToExecute.push({
            id: tc.id,
            name: tc.function.name,
            args: functionArgs,
            isNative: true
          });
        }
      }
      // 2. Kiểm tra nếu model nhả thẻ <tool_call> dạng XML trong text content
      else if (responseMessage.content && responseMessage.content.includes('<tool_call>')) {
        const parsed = parseTextToolCalls(responseMessage.content);
        for (let i = 0; i < parsed.length; i++) {
          toolCallsToExecute.push({
            id: `call_${Date.now()}_${i}`,
            name: parsed[i].name,
            args: parsed[i].args,
            isNative: false
          });
        }
      }

      // Nếu không có yêu cầu gọi thêm công cụ nào -> đã có câu trả lời hoàn chỉnh
      if (toolCallsToExecute.length === 0) {
        let cleanReply = sanitizeAiReply(responseMessage.content || '', collectedFunctionResults);
        if (!cleanReply) {
          cleanReply = 'Dạ KittyBot đã tra cứu dữ liệu nhưng chưa tìm thấy dòng máy hoàn toàn trùng khớp với tiêu chí này trong kho KittyPhone. Bạn có thể cho KittyBot biết thêm về tầm giá hoặc thương hiệu yêu thích không ạ?';
        }

        return {
          reply: cleanReply,
          engine: usedTools
            ? `Groq Online AI (${modelName}) + Function Calling`
            : `Groq Online AI (${modelName})`,
          tier: 1
        };
      }

      // Có công cụ cần chạy: Đưa message của AI vào context và thực thi
      usedTools = true;
      messages.push(responseMessage);

      for (const tc of toolCallsToExecute) {
        console.log(`[AI Function Calling] Lượt ${iterations} - Thực thi hàm ${tc.name}:`, tc.args);
        const functionResult = await executeFunctionCall(tc.name, tc.args);
        collectedFunctionResults.push(functionResult);

        const resultString = JSON.stringify(functionResult);

        if (tc.isNative) {
          messages.push({
            tool_call_id: tc.id,
            role: 'tool',
            name: tc.name,
            content: resultString
          });
        } else {
          messages.push({
            role: 'user',
            content: `[Hệ thống CSDL KittyPhone trả lời hàm ${tc.name}]: ${resultString}\n\nLƯU Ý: Chỉ sử dụng DUY NHẤT dữ liệu trong kết quả trên để trả lời. KHÔNG thêm thông tin nào ngoài dữ liệu này.`
          });
        }
      }
    }

    // Nếu đạt giới hạn vòng lặp, yêu cầu AI tổng hợp câu trả lời cuối cùng
    const FINAL_INSTRUCTION = `\n\nHÃY NHỚ: Chỉ trình bày thông tin sản phẩm từ kết quả tra cứu ở trên. KHÔNG bịa thêm sản phẩm, giá, hay thông số nào không có trong dữ liệu.`;
    messages.push({ role: 'user', content: FINAL_INSTRUCTION });

    const finalResponse = await groq.chat.completions.create({
      model: modelName,
      messages: messages,
      temperature: 0.3,
      max_tokens: 1024
    });

    let finalCleanReply = sanitizeAiReply(finalResponse.choices[0].message.content || '', collectedFunctionResults);
    if (!finalCleanReply) {
      finalCleanReply = 'Dạ KittyBot đã cập nhật thông tin kho hàng và sản phẩm mới nhất. Bạn cần KittyBot tư vấn thêm chi tiết về dòng máy nào không ạ?';
    }

    return {
      reply: finalCleanReply,
      engine: `Groq Online AI (${modelName}) + Function Calling`,
      tier: 1
    };
  } catch (error) {
    console.warn(`[AI Coordinator] Lỗi kết nối Groq Cloud (${error.message}). Tự động Fallback sang Tầng 2 (Rule-Based Engine Offline)!`);
    const fallbackReply = await ruleBasedService.processRuleBased(userMessage);
    return {
      reply: fallbackReply,
      engine: 'Rule-Based Engine (Fallback Offline 0đ)',
      tier: 2
    };
  }
}

module.exports = {
  processChatMessage
};
